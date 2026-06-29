using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 返利计算引擎 — 核心算法
/// </summary>
public class PolicyRebateCalcEngine
{
    private readonly IRepository<ExpPolicyRebate> _rebateRepo;
    private readonly IRepository<ExpPolicyRebateTier> _tierRepo;
    private readonly IRepository<ExpPolicyRebateRule> _ruleRepo;
    private readonly IRepository<ExpPolicyRebateRuleItem> _ruleItemRepo;

    public PolicyRebateCalcEngine(
        IRepository<ExpPolicyRebate> rebateRepo,
        IRepository<ExpPolicyRebateTier> tierRepo,
        IRepository<ExpPolicyRebateRule> ruleRepo,
        IRepository<ExpPolicyRebateRuleItem> ruleItemRepo)
    {
        _rebateRepo = rebateRepo;
        _tierRepo = tierRepo;
        _ruleRepo = ruleRepo;
        _ruleItemRepo = ruleItemRepo;
    }

    /// <summary>
    /// 计算基础返利
    /// </summary>
    /// <param name="policyRebateId">返利方案ID</param>
    /// <param name="dailyVolumes">每天的单量列表</param>
    /// <returns>基础返利金额</returns>
    public async Task<decimal> CalculateBaseRebateAsync(long policyRebateId, List<DailyWaybillSummary> dailyVolumes)
    {
        var policy = await _rebateRepo.GetByIdAsync(policyRebateId);
        if (policy == null) return 0;

        if (policy.FRebateMode == 1)
        {
            // 通票模式：总单量 × 通票返利金额
            var totalWaybills = dailyVolumes.Sum(d => d.Count);
            return totalWaybills * (policy.FFlatRebateAmount ?? 0);
        }

        // 阶梯模式
        var tiers = await _tierRepo.Query()
            .Where(t => t.FPolicyRebateId == policyRebateId)
            .OrderBy(t => t.FSortOrder)
            .ToListAsync();

        if (tiers.Count == 0) return 0;

        decimal totalRebate = 0;
        foreach (var day in dailyVolumes)
        {
            totalRebate += CalculateTieredRebateForDay(day.Count, tiers);
        }
        return totalRebate;
    }

    /// <summary>
    /// 按日单量分段累加计算阶梯返利
    /// </summary>
    private static decimal CalculateTieredRebateForDay(int dailyCount, List<ExpPolicyRebateTier> tiers)
    {
        decimal rebate = 0;
        int remaining = dailyCount;

        for (int i = 0; i < tiers.Count && remaining > 0; i++)
        {
            var tier = tiers[i];
            int tierFrom = tier.FDailyVolumeFrom;
            int? tierTo = tier.FDailyVolumeTo;

            if (dailyCount < tierFrom) break;

            int tierCapacity;
            if (tierTo.HasValue)
            {
                tierCapacity = tierTo.Value - tierFrom + 1;
            }
            else
            {
                // 最高档位，无上限
                tierCapacity = remaining;
            }

            int countInTier = Math.Min(remaining, tierCapacity);
            rebate += countInTier * tier.FRebatePerTicket;
            remaining -= countInTier;
        }

        // 超出最高档位的部分使用最高档位的价格
        if (remaining > 0 && tiers.Count > 0)
        {
            rebate += remaining * tiers[^1].FRebatePerTicket;
        }

        return rebate;
    }

    /// <summary>
    /// 计算5种奖罚调整
    /// </summary>
    public async Task<List<AdjustmentDetail>> CalculateAdjustmentsAsync(
        long policyRebateId, WaybillStatistics stats, decimal baseRebate)
    {
        var rules = await _ruleRepo.Query()
            .Where(r => r.FPolicyRebateId == policyRebateId && r.FEnabled)
            .OrderBy(r => r.FSortOrder)
            .ToListAsync();

        if (rules.Count == 0) return new List<AdjustmentDetail>();

        var ruleIds = rules.Select(r => r.FID).ToList();
        var allItems = await _ruleItemRepo.Query()
            .Where(ri => ruleIds.Contains(ri.FRuleId))
            .OrderBy(ri => ri.FSortOrder)
            .ToListAsync();
        var itemsByRule = allItems.GroupBy(ri => ri.FRuleId).ToDictionary(g => g.Key, g => g.ToList());

        var adjustments = new List<AdjustmentDetail>();

        foreach (var rule in rules)
        {
            var items = itemsByRule.GetValueOrDefault(rule.FID, new());
            var ruleAdjustments = rule.FRuleType switch
            {
                1 => CalculateAvgWeightAdjustments(rule, items, stats, baseRebate),
                2 => CalculateVolumeAdjustments(rule, items, stats, baseRebate),
                3 => CalculateWeightSegmentAdjustments(rule, items, stats, baseRebate),
                4 => CalculateDestinationAdjustments(rule, items, stats, baseRebate),
                5 => CalculateBubbleAdjustments(rule, items, stats, baseRebate),
                _ => new List<AdjustmentDetail>()
            };
            adjustments.AddRange(ruleAdjustments);
        }

        return adjustments;
    }

    /// <summary>
    /// 最终返利 = BaseRebate + TotalReward - TotalPenalty
    /// </summary>
    public decimal CalculateFinalRebate(decimal baseRebate, List<AdjustmentDetail> adjustments)
    {
        var totalReward = adjustments.Where(a => a.AdjustType == 1).Sum(a => a.AdjustAmount);
        var totalPenalty = adjustments.Where(a => a.AdjustType == 2).Sum(a => a.AdjustAmount);
        return baseRebate + totalReward - totalPenalty;
    }

    #region 5种奖罚规则计算

    /// <summary>RuleType=1: 均重奖罚 — 比较平均重量与阈值</summary>
    private static List<AdjustmentDetail> CalculateAvgWeightAdjustments(
        ExpPolicyRebateRule rule, List<ExpPolicyRebateRuleItem> items,
        WaybillStatistics stats, decimal baseRebate)
    {
        var result = new List<AdjustmentDetail>();
        foreach (var item in items)
        {
            bool matched = IsInRange(stats.AvgWeight, item.FThresholdLower, item.FThresholdUpper);
            if (!matched) continue;

            var amount = CalcAdjustAmount(item, stats.TotalWaybills, baseRebate);
            result.Add(new AdjustmentDetail
            {
                RuleId = rule.FID,
                RuleItemId = item.FID,
                RuleName = rule.FRuleName,
                RuleType = rule.FRuleType,
                ActualValue = stats.AvgWeight,
                AdjustType = item.FAdjustType ?? 1,
                AdjustAmount = amount
            });
        }
        return result;
    }

    /// <summary>RuleType=2: 单量奖罚 — 比较日均单量与阈值</summary>
    private static List<AdjustmentDetail> CalculateVolumeAdjustments(
        ExpPolicyRebateRule rule, List<ExpPolicyRebateRuleItem> items,
        WaybillStatistics stats, decimal baseRebate)
    {
        var result = new List<AdjustmentDetail>();
        var dailyAvgVolume = stats.TotalDays > 0 ? (decimal)stats.TotalWaybills / stats.TotalDays : 0;

        foreach (var item in items)
        {
            bool matched = IsInRange(dailyAvgVolume, item.FThresholdLower, item.FThresholdUpper);
            if (!matched) continue;

            var amount = CalcAdjustAmount(item, stats.TotalWaybills, baseRebate);
            result.Add(new AdjustmentDetail
            {
                RuleId = rule.FID,
                RuleItemId = item.FID,
                RuleName = rule.FRuleName,
                RuleType = rule.FRuleType,
                ActualValue = dailyAvgVolume,
                AdjustType = item.FAdjustType ?? 1,
                AdjustAmount = amount
            });
        }
        return result;
    }

    /// <summary>RuleType=3: 重量段占比奖罚 — 按重量段分布与阈值比较</summary>
    private static List<AdjustmentDetail> CalculateWeightSegmentAdjustments(
        ExpPolicyRebateRule rule, List<ExpPolicyRebateRuleItem> items,
        WaybillStatistics stats, decimal baseRebate)
    {
        var result = new List<AdjustmentDetail>();
        if (stats.TotalWaybills == 0) return result;

        foreach (var item in items)
        {
            if (item.FWeightFrom == null && item.FWeightTo == null) continue;

            // 计算该重量段的运单占比
            var segmentCount = stats.WeightSegments
                .Where(ws =>
                    (item.FWeightFrom == null || ws.WeightFrom >= item.FWeightFrom) &&
                    (item.FWeightTo == null || ws.WeightTo <= item.FWeightTo))
                .Sum(ws => ws.Count);

            decimal ratio = (decimal)segmentCount / stats.TotalWaybills * 100;

            bool matched = IsInRange(ratio, item.FThresholdLower, item.FThresholdUpper);
            if (!matched) continue;

            var amount = CalcAdjustAmount(item, stats.TotalWaybills, baseRebate);
            result.Add(new AdjustmentDetail
            {
                RuleId = rule.FID,
                RuleItemId = item.FID,
                RuleName = rule.FRuleName,
                RuleType = rule.FRuleType,
                ActualValue = ratio,
                AdjustType = item.FAdjustType ?? 1,
                AdjustAmount = amount
            });
        }
        return result;
    }

    /// <summary>RuleType=4: 目的地流向占比奖罚 — 按省份分布与阈值比较</summary>
    private static List<AdjustmentDetail> CalculateDestinationAdjustments(
        ExpPolicyRebateRule rule, List<ExpPolicyRebateRuleItem> items,
        WaybillStatistics stats, decimal baseRebate)
    {
        var result = new List<AdjustmentDetail>();
        if (stats.TotalWaybills == 0) return result;

        foreach (var item in items)
        {
            if (item.FProvinceId == null) continue;

            var provinceCount = stats.ProvinceDistribution
                .Where(pd => pd.ProvinceId == item.FProvinceId)
                .Sum(pd => pd.Count);

            decimal ratio = (decimal)provinceCount / stats.TotalWaybills * 100;

            bool matched = IsInRange(ratio, item.FThresholdLower, item.FThresholdUpper);
            if (!matched) continue;

            var amount = CalcAdjustAmount(item, stats.TotalWaybills, baseRebate);
            result.Add(new AdjustmentDetail
            {
                RuleId = rule.FID,
                RuleItemId = item.FID,
                RuleName = rule.FRuleName,
                RuleType = rule.FRuleType,
                ActualValue = ratio,
                AdjustType = item.FAdjustType ?? 1,
                AdjustAmount = amount
            });
        }
        return result;
    }

    /// <summary>RuleType=5: 计泡规则 — 泡货占比（抛重>实重的运单），与阈值比较</summary>
    private static List<AdjustmentDetail> CalculateBubbleAdjustments(
        ExpPolicyRebateRule rule, List<ExpPolicyRebateRuleItem> items,
        WaybillStatistics stats, decimal baseRebate)
    {
        var result = new List<AdjustmentDetail>();
        if (stats.TotalWaybills == 0) return result;

        decimal bubbleRatio = (decimal)stats.BubbleWaybillCount / stats.TotalWaybills * 100;

        foreach (var item in items)
        {
            bool matched = IsInRange(bubbleRatio, item.FThresholdLower, item.FThresholdUpper);
            if (!matched) continue;

            var amount = CalcAdjustAmount(item, stats.TotalWaybills, baseRebate);
            result.Add(new AdjustmentDetail
            {
                RuleId = rule.FID,
                RuleItemId = item.FID,
                RuleName = rule.FRuleName,
                RuleType = rule.FRuleType,
                ActualValue = bubbleRatio,
                AdjustType = item.FAdjustType ?? 1,
                AdjustAmount = amount
            });
        }
        return result;
    }

    #endregion

    #region Helper

    private static bool IsInRange(decimal value, decimal? lower, decimal? upper)
    {
        if (lower.HasValue && value < lower.Value) return false;
        if (upper.HasValue && value > upper.Value) return false;
        return true;
    }

    /// <summary>
    /// 计算调整金额：CalcMethod=1 → 每票固定金额 × 运单数；=2 → BaseRebate × 比例
    /// </summary>
    private static decimal CalcAdjustAmount(ExpPolicyRebateRuleItem item, int totalWaybills, decimal baseRebate)
    {
        if (item.FAdjustCalcMethod == 1)
        {
            return (item.FAdjustAmount ?? 0) * totalWaybills;
        }
        if (item.FAdjustCalcMethod == 2)
        {
            return baseRebate * (item.FAdjustRate ?? 0);
        }
        return 0;
    }

    #endregion
}

#region 数据模型

/// <summary>每日运单汇总</summary>
public class DailyWaybillSummary
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
    public decimal TotalWeight { get; set; }
}

/// <summary>运单统计数据</summary>
public class WaybillStatistics
{
    public int TotalWaybills { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal AvgWeight { get; set; }
    public int TotalDays { get; set; }
    /// <summary>泡货运单数（抛重>实重）</summary>
    public int BubbleWaybillCount { get; set; }
    /// <summary>重量段分布</summary>
    public List<WeightSegmentStat> WeightSegments { get; set; } = new();
    /// <summary>省份分布</summary>
    public List<ProvinceDistributionStat> ProvinceDistribution { get; set; } = new();
}

public class WeightSegmentStat
{
    public decimal WeightFrom { get; set; }
    public decimal WeightTo { get; set; }
    public int Count { get; set; }
}

public class ProvinceDistributionStat
{
    public int ProvinceId { get; set; }
    public int Count { get; set; }
}

/// <summary>奖罚调整明细</summary>
public class AdjustmentDetail
{
    public long RuleId { get; set; }
    public long RuleItemId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public int RuleType { get; set; }
    public decimal ActualValue { get; set; }
    public int AdjustType { get; set; }
    public decimal AdjustAmount { get; set; }
}

#endregion
