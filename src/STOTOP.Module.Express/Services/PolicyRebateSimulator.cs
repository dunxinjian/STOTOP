using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 返利测算模拟器
/// </summary>
public class PolicyRebateSimulator
{
    private readonly IRepository<ExpPolicyRebate> _rebateRepo;
    private readonly IRepository<ExpWaybill> _waybillRepo;
    private readonly PolicyRebateCalcEngine _calcEngine;

    public PolicyRebateSimulator(
        IRepository<ExpPolicyRebate> rebateRepo,
        IRepository<ExpWaybill> waybillRepo,
        PolicyRebateCalcEngine calcEngine)
    {
        _rebateRepo = rebateRepo;
        _waybillRepo = waybillRepo;
        _calcEngine = calcEngine;
    }

    /// <summary>
    /// 执行测算
    /// </summary>
    public async Task<SimulationResult> SimulateAsync(SimulationRequest request)
    {
        if (request.UseHistory)
            return await SimulateWithHistoryAsync(request);
        return SimulateWithAssumptions(request);
    }

    /// <summary>基于历史运单数据测算</summary>
    private async Task<SimulationResult> SimulateWithHistoryAsync(SimulationRequest request)
    {
        var policy = await _rebateRepo.GetByIdAsync(request.PolicyRebateId)
            ?? throw new InvalidOperationException("返利方案不存在");

        if (!request.PeriodStart.HasValue || !request.PeriodEnd.HasValue)
            throw new InvalidOperationException("历史测算需要指定账期范围");

        var waybills = await _waybillRepo.Query()
            .Where(w => w.FBrandCode == policy.FBrandCode
                && w.FWaybillDate >= request.PeriodStart.Value
                && w.FWaybillDate <= request.PeriodEnd.Value)
            .ToListAsync();

        var dailyVolumes = waybills
            .GroupBy(w => w.FWaybillDate.Date)
            .Select(g => new DailyWaybillSummary
            {
                Date = g.Key,
                Count = g.Count(),
                TotalWeight = g.Sum(w => w.FBillableWeight ?? w.FActualWeight ?? 0)
            })
            .ToList();

        var totalWaybills = waybills.Count;
        var totalWeight = waybills.Sum(w => w.FBillableWeight ?? w.FActualWeight ?? 0);
        var avgWeight = totalWaybills > 0 ? totalWeight / totalWaybills : 0;

        var bubbleCount = waybills.Count(w =>
            w.FVolumetricWeight.HasValue && w.FActualWeight.HasValue
            && w.FVolumetricWeight.Value > w.FActualWeight.Value);

        var weightBuckets = new[] { (0m, 1m), (1m, 3m), (3m, 5m), (5m, 10m), (10m, 20m), (20m, 999m) };
        var weightSegments = weightBuckets.Select(b => new WeightSegmentStat
        {
            WeightFrom = b.Item1,
            WeightTo = b.Item2,
            Count = waybills.Count(w =>
            {
                var weight = w.FBillableWeight ?? w.FActualWeight ?? 0;
                return weight >= b.Item1 && weight < b.Item2;
            })
        }).ToList();

        var provinceDist = waybills
            .Where(w => w.FReceiverProvinceId.HasValue)
            .GroupBy(w => w.FReceiverProvinceId!.Value)
            .Select(g => new ProvinceDistributionStat { ProvinceId = g.Key, Count = g.Count() })
            .ToList();

        var stats = new WaybillStatistics
        {
            TotalWaybills = totalWaybills,
            TotalWeight = totalWeight,
            AvgWeight = avgWeight,
            TotalDays = dailyVolumes.Count,
            BubbleWaybillCount = bubbleCount,
            WeightSegments = weightSegments,
            ProvinceDistribution = provinceDist
        };

        var baseRebate = await _calcEngine.CalculateBaseRebateAsync(request.PolicyRebateId, dailyVolumes);
        var adjustments = await _calcEngine.CalculateAdjustmentsAsync(request.PolicyRebateId, stats, baseRebate);

        return BuildResult(totalWaybills, totalWeight, avgWeight, baseRebate, adjustments);
    }

    /// <summary>基于假设数据测算</summary>
    private SimulationResult SimulateWithAssumptions(SimulationRequest request)
    {
        var dailyVolume = request.AssumedDailyVolume ?? 100;
        var days = request.AssumedDays ?? 30;
        var avgWeight = request.AssumedAvgWeight ?? 1.5m;

        var totalWaybills = dailyVolume * days;
        var totalWeight = totalWaybills * avgWeight;

        // 简化模拟：假设无奖罚
        return new SimulationResult
        {
            TotalWaybills = totalWaybills,
            TotalWeight = totalWeight,
            AvgWeight = avgWeight,
            BaseRebateAmount = 0, // 需要异步计算，简化实现返回0
            TotalReward = 0,
            TotalPenalty = 0,
            FinalRebateAmount = 0,
            Adjustments = new()
        };
    }

    private static SimulationResult BuildResult(int totalWaybills, decimal totalWeight, decimal avgWeight,
        decimal baseRebate, List<AdjustmentDetail> adjustments)
    {
        var totalReward = adjustments.Where(a => a.AdjustType == 1).Sum(a => a.AdjustAmount);
        var totalPenalty = adjustments.Where(a => a.AdjustType == 2).Sum(a => a.AdjustAmount);

        return new SimulationResult
        {
            TotalWaybills = totalWaybills,
            TotalWeight = totalWeight,
            AvgWeight = avgWeight,
            BaseRebateAmount = baseRebate,
            TotalReward = totalReward,
            TotalPenalty = totalPenalty,
            FinalRebateAmount = baseRebate + totalReward - totalPenalty,
            Adjustments = adjustments.Select(a => new SimulationAdjustDetail
            {
                RuleName = a.RuleName,
                RuleType = a.RuleType,
                ActualValue = a.ActualValue,
                AdjustType = a.AdjustType,
                AdjustAmount = a.AdjustAmount
            }).ToList()
        };
    }
}
