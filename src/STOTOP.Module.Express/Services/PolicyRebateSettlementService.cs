using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class PolicyRebateSettlementService : IPolicyRebateSettlementService
{
    private readonly IRepository<ExpPolicyRebate> _rebateRepo;
    private readonly IRepository<ExpPolicyRebateSettlement> _settlementRepo;
    private readonly IRepository<ExpPolicyRebateSettlementDetail> _detailRepo;
    private readonly IRepository<ExpWaybill> _waybillRepo;
    private readonly PolicyRebateCalcEngine _calcEngine;

    public PolicyRebateSettlementService(
        IRepository<ExpPolicyRebate> rebateRepo,
        IRepository<ExpPolicyRebateSettlement> settlementRepo,
        IRepository<ExpPolicyRebateSettlementDetail> detailRepo,
        IRepository<ExpWaybill> waybillRepo,
        PolicyRebateCalcEngine calcEngine)
    {
        _rebateRepo = rebateRepo;
        _settlementRepo = settlementRepo;
        _detailRepo = detailRepo;
        _waybillRepo = waybillRepo;
        _calcEngine = calcEngine;
    }

    public async Task<PolicyRebateSettlementDetailDto> ExecuteSettlementAsync(
        long policyRebateId, DateTime periodStart, DateTime periodEnd)
    {
        var policy = await _rebateRepo.GetByIdAsync(policyRebateId)
            ?? throw new InvalidOperationException("返利方案不存在");

        // 1. 查询账期内运单数据
        var waybills = await _waybillRepo.Query()
            .Where(w => w.FBrandCode == policy.FBrandCode
                && w.FWaybillDate >= periodStart
                && w.FWaybillDate <= periodEnd)
            .ToListAsync();

        // 按日分组
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
        var totalDays = dailyVolumes.Count;

        // 泡货统计（抛重>实重）
        var bubbleCount = waybills.Count(w =>
            w.FVolumetricWeight.HasValue && w.FActualWeight.HasValue
            && w.FVolumetricWeight.Value > w.FActualWeight.Value);

        // 重量段分布（按0-1, 1-3, 3-5, 5-10, 10-20, 20+ 分段）
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

        // 省份分布
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
            TotalDays = totalDays,
            BubbleWaybillCount = bubbleCount,
            WeightSegments = weightSegments,
            ProvinceDistribution = provinceDist
        };

        // 2. 计算基础返利
        var baseRebate = await _calcEngine.CalculateBaseRebateAsync(policyRebateId, dailyVolumes);

        // 3. 计算奖罚调整
        var adjustments = await _calcEngine.CalculateAdjustmentsAsync(policyRebateId, stats, baseRebate);

        var totalReward = adjustments.Where(a => a.AdjustType == 1).Sum(a => a.AdjustAmount);
        var totalPenalty = adjustments.Where(a => a.AdjustType == 2).Sum(a => a.AdjustAmount);
        var finalRebate = _calcEngine.CalculateFinalRebate(baseRebate, adjustments);

        // 4. 创建结算记录
        var settlement = new ExpPolicyRebateSettlement
        {
            FPolicyRebateId = policyRebateId,
            FBrandCode = policy.FBrandCode,
            FPeriodStart = periodStart,
            FPeriodEnd = periodEnd,
            FTotalWaybills = totalWaybills,
            FTotalWeight = totalWeight,
            FAvgWeight = avgWeight,
            FBaseRebateAmount = baseRebate,
            FTotalReward = totalReward,
            FTotalPenalty = totalPenalty,
            FFinalRebateAmount = finalRebate,
            FStatus = 0,
            FCreatedTime = DateTime.Now
        };
        settlement = await _settlementRepo.AddAsync(settlement);

        // 创建明细
        var details = new List<ExpPolicyRebateSettlementDetail>();
        foreach (var adj in adjustments)
        {
            var detail = new ExpPolicyRebateSettlementDetail
            {
                FSettlementId = settlement.FID,
                FRuleId = adj.RuleId,
                FRuleItemId = adj.RuleItemId,
                FActualValue = adj.ActualValue,
                FThresholdValue = null,
                FAdjustType = adj.AdjustType,
                FAdjustAmount = adj.AdjustAmount,
                FRemark = $"{adj.RuleName}: 实际值={adj.ActualValue:F4}"
            };
            detail = await _detailRepo.AddAsync(detail);
            details.Add(detail);
        }

        return MapToDetailDto(settlement, details);
    }

    public async Task<PagedResult<PolicyRebateSettlementListItemDto>> GetPagedListAsync(SettlementQueryRequest request)
    {
        var query = _settlementRepo.Query();

        if (request.PolicyRebateId.HasValue)
            query = query.Where(e => e.FPolicyRebateId == request.PolicyRebateId.Value);
        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(e => e.FBrandCode == request.BrandCode);
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);
        if (request.PeriodStartFrom.HasValue)
            query = query.Where(e => e.FPeriodStart >= request.PeriodStartFrom.Value);
        if (request.PeriodStartTo.HasValue)
            query = query.Where(e => e.FPeriodStart <= request.PeriodStartTo.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<PolicyRebateSettlementListItemDto>
        {
            Items = items.Select(MapToListItem).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<PolicyRebateSettlementDetailDto?> GetDetailAsync(long id)
    {
        var settlement = await _settlementRepo.GetByIdAsync(id);
        if (settlement == null) return null;

        var details = await _detailRepo.Query()
            .Where(d => d.FSettlementId == id)
            .ToListAsync();

        return MapToDetailDto(settlement, details);
    }

    public async Task<bool> ConfirmAsync(long id, string confirmedBy)
    {
        var entity = await _settlementRepo.Query().AsTracking().FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null || entity.FStatus != 0) return false;
        entity.FStatus = 1;
        entity.FConfirmedBy = confirmedBy;
        entity.FConfirmedTime = DateTime.Now;
        await _settlementRepo.UpdateAsync(entity);
        return true;
    }

    public async Task<bool> WriteOffAsync(long id)
    {
        var entity = await _settlementRepo.Query().AsTracking().FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null || entity.FStatus != 1) return false;
        entity.FStatus = 2;
        await _settlementRepo.UpdateAsync(entity);
        return true;
    }

    #region Mapping

    private static PolicyRebateSettlementListItemDto MapToListItem(ExpPolicyRebateSettlement e) => new()
    {
        Id = e.FID,
        PolicyRebateId = e.FPolicyRebateId,
        BrandCode = e.FBrandCode,
        PeriodStart = e.FPeriodStart,
        PeriodEnd = e.FPeriodEnd,
        TotalWaybills = e.FTotalWaybills,
        TotalWeight = e.FTotalWeight,
        BaseRebateAmount = e.FBaseRebateAmount,
        FinalRebateAmount = e.FFinalRebateAmount,
        Status = e.FStatus,
        CreatedTime = e.FCreatedTime
    };

    private static PolicyRebateSettlementDetailDto MapToDetailDto(
        ExpPolicyRebateSettlement e, List<ExpPolicyRebateSettlementDetail> details) => new()
    {
        Id = e.FID,
        PolicyRebateId = e.FPolicyRebateId,
        BrandCode = e.FBrandCode,
        PeriodStart = e.FPeriodStart,
        PeriodEnd = e.FPeriodEnd,
        TotalWaybills = e.FTotalWaybills,
        TotalWeight = e.FTotalWeight,
        AvgWeight = e.FAvgWeight,
        BaseRebateAmount = e.FBaseRebateAmount,
        TotalReward = e.FTotalReward,
        TotalPenalty = e.FTotalPenalty,
        FinalRebateAmount = e.FFinalRebateAmount,
        Status = e.FStatus,
        ConfirmedBy = e.FConfirmedBy,
        ConfirmedTime = e.FConfirmedTime,
        Remark = e.FRemark,
        CreatedTime = e.FCreatedTime,
        Details = details.Select(d => new SettlementAdjustDetailDto
        {
            Id = d.FID,
            RuleId = d.FRuleId,
            RuleItemId = d.FRuleItemId,
            ActualValue = d.FActualValue,
            ThresholdValue = d.FThresholdValue,
            AdjustType = d.FAdjustType,
            AdjustAmount = d.FAdjustAmount,
            Remark = d.FRemark
        }).ToList()
    };

    #endregion
}
