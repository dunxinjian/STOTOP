using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.KSF.Dtos;
using STOTOP.Module.KSF.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.KSF.Services;

public class KsfPlanService : IKsfPlanService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<KsfPlanService> _logger;

    public KsfPlanService(STOTOPDbContext context, ILogger<KsfPlanService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResult<List<KsfPlanDto>>> GetListAsync(long orgId, int? runMode = null)
    {
        var query = _context.Set<KsfPlan>().AsQueryable();
        if (runMode.HasValue)
            query = query.Where(p => p.F运行模式 == runMode.Value);

        var plans = await query
            .OrderByDescending(p => p.FID)
            .ToListAsync();

        if (plans.Count == 0)
            return ApiResult<List<KsfPlanDto>>.Success(new List<KsfPlanDto>());

        var planIds = plans.Select(p => p.FID).ToList();
        var positionIds = plans.Select(p => p.F岗位ID).Distinct().ToList();

        var details = await _context.Set<KsfPlanDetail>()
            .Where(d => planIds.Contains(d.F方案ID))
            .ToListAsync();

        var indicatorIds = details.Select(d => d.F指标ID).Distinct().ToList();
        var indicators = await _context.Set<KsfIndicator>()
            .Where(i => indicatorIds.Contains(i.FID))
            .Select(i => new { i.FID, i.F编码, i.F名称, i.F计量单位 })
            .ToListAsync();
        var indicatorMap = indicators.ToDictionary(i => i.FID);

        var positions = await _context.Set<SysPosition>()
            .Where(p => positionIds.Contains(p.FID))
            .Select(p => new { p.FID, p.FName })
            .ToListAsync();
        var positionMap = positions.ToDictionary(p => p.FID, p => p.FName);

        var detailDtoMap = details.GroupBy(d => d.F方案ID).ToDictionary(
            g => g.Key,
            g => g.Select(d => new KsfPlanDetailDto
            {
                Id = d.FID,
                PlanId = d.F方案ID,
                IndicatorId = d.F指标ID,
                IndicatorCode = indicatorMap.GetValueOrDefault(d.F指标ID)?.F编码,
                IndicatorName = indicatorMap.GetValueOrDefault(d.F指标ID)?.F名称,
                IndicatorUnit = indicatorMap.GetValueOrDefault(d.F指标ID)?.F计量单位,
                Weight = d.F权重,
                Balance = d.F平衡点,
                IncentiveScaleJson = d.F激励刻度JSON,
                MinGuarantee = d.F最低保底
            }).ToList());

        var list = plans.Select(p =>
        {
            var dto = MapToDto(p);
            dto.PositionName = positionMap.GetValueOrDefault(p.F岗位ID);
            dto.Details = detailDtoMap.GetValueOrDefault(p.FID, new List<KsfPlanDetailDto>());
            return dto;
        }).ToList();

        return ApiResult<List<KsfPlanDto>>.Success(list);
    }

    public async Task<ApiResult<KsfPlanDto>> CreateAsync(long orgId, KsfPlanCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResult<KsfPlanDto>.Fail("方案名称不能为空");
        if (request.PositionId <= 0)
            return ApiResult<KsfPlanDto>.Fail("必须指定关联岗位");
        if (request.Details == null || request.Details.Count == 0)
            return ApiResult<KsfPlanDto>.Fail("方案明细不能为空");

        var totalWeight = request.Details.Sum(d => d.Weight);
        if (totalWeight <= 0)
            return ApiResult<KsfPlanDto>.Fail("方案明细权重总和必须大于 0");

        var plan = new KsfPlan
        {
            FOrgId = orgId,
            F名称 = request.Name.Trim(),
            F岗位ID = request.PositionId,
            F生效起期 = request.EffectiveFrom,
            F生效止期 = request.EffectiveTo,
            F启用状态 = request.IsEnabled,
            F运行模式 = request.RunMode,
            F门槛规则JSON = request.ThresholdRulesJson,
            F岗位月加薪基数 = request.MonthlyRaiseBase,
            F负责人ID = request.OwnerId,
            F创建时间 = DateTime.Now,
            F更新时间 = DateTime.Now
        };

        _context.Set<KsfPlan>().Add(plan);
        await _context.SaveChangesAsync();

        foreach (var d in request.Details)
        {
            _context.Set<KsfPlanDetail>().Add(new KsfPlanDetail
            {
                FOrgId = orgId,
                F方案ID = plan.FID,
                F指标ID = d.IndicatorId,
                F权重 = d.Weight,
                F平衡点 = d.Balance,
                F激励刻度JSON = d.IncentiveScaleJson,
                F最低保底 = d.MinGuarantee
            });
        }
        await _context.SaveChangesAsync();

        return await BuildSingleAsync(plan.FID);
    }

    public async Task<ApiResult<KsfPlanDto>> UpdateAsync(long orgId, long id, KsfPlanCreateRequest request)
    {
        var plan = await _context.Set<KsfPlan>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);
        if (plan == null)
            return ApiResult<KsfPlanDto>.Fail("方案不存在");

        if (plan.F运行模式 == 1)
            return ApiResult<KsfPlanDto>.Fail("正式方案禁止编辑，请创建新方案");

        plan.F名称 = request.Name.Trim();
        plan.F岗位ID = request.PositionId;
        plan.F生效起期 = request.EffectiveFrom;
        plan.F生效止期 = request.EffectiveTo;
        plan.F启用状态 = request.IsEnabled;
        plan.F运行模式 = request.RunMode;
        plan.F门槛规则JSON = request.ThresholdRulesJson;
        plan.F岗位月加薪基数 = request.MonthlyRaiseBase;
        plan.F负责人ID = request.OwnerId;
        plan.F更新时间 = DateTime.Now;

        // 全量替换明细
        var oldDetails = await _context.Set<KsfPlanDetail>()
            .Where(d => d.F方案ID == id)
            .ToListAsync();
        if (oldDetails.Count > 0)
            _context.Set<KsfPlanDetail>().RemoveRange(oldDetails);

        foreach (var d in request.Details)
        {
            _context.Set<KsfPlanDetail>().Add(new KsfPlanDetail
            {
                FOrgId = orgId,
                F方案ID = plan.FID,
                F指标ID = d.IndicatorId,
                F权重 = d.Weight,
                F平衡点 = d.Balance,
                F激励刻度JSON = d.IncentiveScaleJson,
                F最低保底 = d.MinGuarantee
            });
        }
        await _context.SaveChangesAsync();

        return await BuildSingleAsync(plan.FID);
    }

    public async Task<ApiResult> ActivateAsync(long orgId, long planId, DateTime effectiveFrom)
    {
        var plan = await _context.Set<KsfPlan>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == planId);
        if (plan == null)
            return ApiResult.Fail("方案不存在");
        if (!plan.F启用状态)
            return ApiResult.Fail("方案未启用，无法激活为正式");
        if (plan.F运行模式 == 1)
            return ApiResult.Fail("方案已是正式状态");

        // 校验同岗位、同期间不存在另一个正式方案
        var conflict = await _context.Set<KsfPlan>()
            .Where(p => p.F岗位ID == plan.F岗位ID
                && p.F运行模式 == 1
                && p.FID != planId
                && p.F生效起期 <= effectiveFrom
                && (p.F生效止期 == null || p.F生效止期 >= effectiveFrom))
            .AnyAsync();
        if (conflict)
            return ApiResult.Fail("同岗位在该期间已存在正式方案，请先终止旧方案");

        plan.F运行模式 = 1;
        plan.F生效起期 = effectiveFrom;
        plan.F更新时间 = DateTime.Now;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "[KSF] 方案激活 PlanId={PlanId} OrgId={OrgId} PositionId={PositionId} EffectiveFrom={EffectiveFrom:yyyy-MM-dd}",
            plan.FID, orgId, plan.F岗位ID, effectiveFrom);

        return ApiResult.Ok("已激活为正式方案");
    }

    public async Task<ApiResult<KsfPlanDto?>> GetByPositionAsync(long orgId, long positionId)
    {
        var today = DateTime.Today;
        var plan = await _context.Set<KsfPlan>()
            .Where(p => p.F岗位ID == positionId
                && p.F启用状态
                && p.F运行模式 == 1
                && p.F生效起期 <= today
                && (p.F生效止期 == null || p.F生效止期 >= today))
            .OrderByDescending(p => p.F生效起期)
            .FirstOrDefaultAsync();
        if (plan == null)
            return ApiResult<KsfPlanDto?>.Success(null, "无生效方案");

        var result = await BuildSingleAsync(plan.FID);
        if (result.Code != 200 || result.Data == null)
            return ApiResult<KsfPlanDto?>.Fail(result.Message);
        return ApiResult<KsfPlanDto?>.Success(result.Data);
    }

    private async Task<ApiResult<KsfPlanDto>> BuildSingleAsync(long planId)
    {
        var p = await _context.Set<KsfPlan>().FirstOrDefaultAsync(x => x.FID == planId);
        if (p == null)
            return ApiResult<KsfPlanDto>.Fail("方案不存在");

        var dto = MapToDto(p);

        var posName = await _context.Set<SysPosition>()
            .Where(x => x.FID == p.F岗位ID)
            .Select(x => x.FName)
            .FirstOrDefaultAsync();
        dto.PositionName = posName;

        var details = await _context.Set<KsfPlanDetail>()
            .Where(d => d.F方案ID == planId)
            .ToListAsync();

        var indicatorIds = details.Select(d => d.F指标ID).Distinct().ToList();
        var indicators = await _context.Set<KsfIndicator>()
            .Where(i => indicatorIds.Contains(i.FID))
            .Select(i => new { i.FID, i.F编码, i.F名称, i.F计量单位 })
            .ToListAsync();
        var indicatorMap = indicators.ToDictionary(i => i.FID);

        dto.Details = details.Select(d => new KsfPlanDetailDto
        {
            Id = d.FID,
            PlanId = d.F方案ID,
            IndicatorId = d.F指标ID,
            IndicatorCode = indicatorMap.GetValueOrDefault(d.F指标ID)?.F编码,
            IndicatorName = indicatorMap.GetValueOrDefault(d.F指标ID)?.F名称,
            IndicatorUnit = indicatorMap.GetValueOrDefault(d.F指标ID)?.F计量单位,
            Weight = d.F权重,
            Balance = d.F平衡点,
            IncentiveScaleJson = d.F激励刻度JSON,
            MinGuarantee = d.F最低保底
        }).ToList();

        return ApiResult<KsfPlanDto>.Success(dto);
    }

    private static KsfPlanDto MapToDto(KsfPlan p) => new()
    {
        Id = p.FID,
        OrgId = p.FOrgId,
        Name = p.F名称,
        PositionId = p.F岗位ID,
        EffectiveFrom = p.F生效起期,
        EffectiveTo = p.F生效止期,
        IsEnabled = p.F启用状态,
        RunMode = p.F运行模式,
        ThresholdRulesJson = p.F门槛规则JSON,
        MonthlyRaiseBase = p.F岗位月加薪基数,
        OwnerId = p.F负责人ID,
        CreateTime = p.F创建时间,
        UpdateTime = p.F更新时间
    };
}
