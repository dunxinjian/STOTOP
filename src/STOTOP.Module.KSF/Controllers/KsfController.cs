using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.KSF.Dtos;
using STOTOP.Module.KSF.Services;

namespace STOTOP.Module.KSF.Controllers;

[Authorize]
[ApiController]
[Route("api/ksf")]
public class KsfController : ControllerBase
{
    private readonly IKsfIndicatorService _indicatorService;
    private readonly IKsfPlanService _planService;
    private readonly IKsfResultService _resultService;
    private readonly IKsfMappingService _mappingService;

    public KsfController(
        IKsfIndicatorService indicatorService,
        IKsfPlanService planService,
        IKsfResultService resultService,
        IKsfMappingService mappingService)
    {
        _indicatorService = indicatorService;
        _planService = planService;
        _resultService = resultService;
        _mappingService = mappingService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    // ===== 指标管理 =====

    /// <summary>指标列表</summary>
    [HttpGet("indicators")]
    [RequirePermission(KsfPermissions.IndicatorView)]
    public Task<ApiResult<List<KsfIndicatorDto>>> GetIndicators([FromQuery] bool? enabled = null)
        => _indicatorService.GetListAsync(GetOrgId(), enabled);

    /// <summary>创建指标</summary>
    [HttpPost("indicators")]
    [RequirePermission(KsfPermissions.IndicatorEdit)]
    public Task<ApiResult<KsfIndicatorDto>> CreateIndicator([FromBody] KsfIndicatorCreateRequest request)
        => _indicatorService.CreateAsync(GetOrgId(), request);

    /// <summary>更新指标</summary>
    [HttpPut("indicators/{id:long}")]
    [RequirePermission(KsfPermissions.IndicatorEdit)]
    public Task<ApiResult<KsfIndicatorDto>> UpdateIndicator(long id, [FromBody] KsfIndicatorCreateRequest request)
        => _indicatorService.UpdateAsync(GetOrgId(), id, request);

    /// <summary>删除指标</summary>
    [HttpDelete("indicators/{id:long}")]
    [RequirePermission(KsfPermissions.IndicatorEdit)]
    public Task<ApiResult> DeleteIndicator(long id)
        => _indicatorService.DeleteAsync(GetOrgId(), id);

    /// <summary>校验 SQL 模板安全性</summary>
    [HttpPost("indicators/validate-sql")]
    [RequirePermission(KsfPermissions.IndicatorEdit)]
    public Task<ApiResult<bool>> ValidateSql([FromBody] ValidateSqlRequest request)
        => _indicatorService.ValidateSqlTemplateAsync(request?.Sql ?? string.Empty);

    public class ValidateSqlRequest
    {
        public string Sql { get; set; } = string.Empty;
    }

    // ===== 岗位方案 =====

    /// <summary>方案列表</summary>
    [HttpGet("plans")]
    [RequirePermission(KsfPermissions.PlanView)]
    public Task<ApiResult<List<KsfPlanDto>>> GetPlans([FromQuery] int? runMode = null)
        => _planService.GetListAsync(GetOrgId(), runMode);

    /// <summary>创建方案</summary>
    [HttpPost("plans")]
    [RequirePermission(KsfPermissions.PlanEdit)]
    public Task<ApiResult<KsfPlanDto>> CreatePlan([FromBody] KsfPlanCreateRequest request)
        => _planService.CreateAsync(GetOrgId(), request);

    /// <summary>更新方案（仅试运行可改）</summary>
    [HttpPut("plans/{id:long}")]
    [RequirePermission(KsfPermissions.PlanEdit)]
    public Task<ApiResult<KsfPlanDto>> UpdatePlan(long id, [FromBody] KsfPlanCreateRequest request)
        => _planService.UpdateAsync(GetOrgId(), id, request);

    /// <summary>激活为正式</summary>
    [HttpPost("plans/{id:long}/activate")]
    [RequirePermission(KsfPermissions.PlanActivate)]
    public Task<ApiResult> ActivatePlan(long id, [FromBody] ActivatePlanRequest request)
        => _planService.ActivateAsync(GetOrgId(), id, request?.EffectiveFrom ?? DateTime.Today);

    public class ActivatePlanRequest
    {
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;
    }

    /// <summary>按岗位查询当前生效方案</summary>
    [HttpGet("plans/by-position/{positionId:long}")]
    [RequirePermission(KsfPermissions.PlanView)]
    public Task<ApiResult<KsfPlanDto?>> GetPlanByPosition(long positionId)
        => _planService.GetByPositionAsync(GetOrgId(), positionId);

    // ===== 核算结果 =====

    /// <summary>核算结果列表</summary>
    [HttpGet("results")]
    [RequirePermission(KsfPermissions.ResultView)]
    public Task<ApiResult<List<KsfResultDto>>> GetResults(
        [FromQuery] string? period = null,
        [FromQuery] long? employeeId = null,
        [FromQuery] int? status = null)
        => _resultService.GetListAsync(GetOrgId(), period, employeeId, status);

    /// <summary>核算结果详情（含明细）</summary>
    [HttpGet("results/{id:long}")]
    [RequirePermission(KsfPermissions.ResultView)]
    public Task<ApiResult<KsfResultDto>> GetResultDetail(long id)
        => _resultService.GetDetailAsync(GetOrgId(), id);

    /// <summary>我的核算结果</summary>
    [HttpGet("results/my")]
    [Authorize]
    public Task<ApiResult<List<KsfResultDto>>> GetMyResults([FromQuery] int count = 12)
        => _resultService.GetMyResultsAsync(GetOrgId(), GetUserId(), count);

    // ===== 员工经营单元映射 =====

    /// <summary>员工映射列表</summary>
    [HttpGet("mappings")]
    [RequirePermission(KsfPermissions.MappingView)]
    public Task<ApiResult<List<KsfMappingDto>>> GetMappings([FromQuery] long? employeeId = null)
        => _mappingService.GetListAsync(GetOrgId(), employeeId);

    /// <summary>批量保存员工映射</summary>
    [HttpPost("mappings/batch")]
    [RequirePermission(KsfPermissions.MappingEdit)]
    public Task<ApiResult> BatchSaveMappings([FromBody] List<KsfMappingBatchRequest> mappings)
        => _mappingService.BatchSaveAsync(GetOrgId(), mappings);
}
