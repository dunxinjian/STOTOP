using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/performance")]
public class PerformanceController : ControllerBase
{
    private readonly IPerformanceService _service;

    public PerformanceController(IPerformanceService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    // ===== 考核周期管理 =====

    /// <summary>考核周期列表</summary>
    [HttpGet("periods")]
    [RequirePermission(TaskPermissions.PerformanceView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<PerformancePeriodListDto>>> GetPeriods([FromQuery] PerformancePeriodPagedRequest query)
    {
        return await _service.GetPeriodsPagedAsync(query, GetOrgId());
    }

    /// <summary>创建考核周期</summary>
    [HttpPost("periods")]
    [RequirePermission(TaskPermissions.PerformanceManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<PerformancePeriodListDto>> CreatePeriod([FromBody] CreatePerformancePeriodRequest request)
    {
        return await _service.CreatePeriodAsync(request, GetOrgId(), GetUserId());
    }

    /// <summary>更新考核周期</summary>
    [HttpPut("periods/{id}")]
    [RequirePermission(TaskPermissions.PerformanceManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<PerformancePeriodListDto>> UpdatePeriod(long id, [FromBody] UpdatePerformancePeriodRequest request)
    {
        return await _service.UpdatePeriodAsync(id, request);
    }

    /// <summary>触发该周期绩效自动计算</summary>
    [HttpPost("periods/{id}/calculate")]
    [RequirePermission(TaskPermissions.PerformanceManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Calculate(long id)
    {
        return await _service.CalculateAsync(id);
    }

    /// <summary>获取周期内所有考核记录</summary>
    [HttpGet("periods/{id}/records")]
    [RequirePermission(TaskPermissions.PerformanceView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<PerformanceRecordListDto>>> GetRecordsByPeriod(long id)
    {
        return await _service.GetRecordsByPeriodAsync(id);
    }

    // ===== 考核记录 =====

    /// <summary>获取个人考核详情（含任务明细+维度评分）</summary>
    [HttpGet("records/{id}")]
    [RequirePermission(TaskPermissions.PerformanceView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PerformanceRecordDetailDto>> GetRecordDetail(long id)
    {
        return await _service.GetRecordDetailAsync(id);
    }

    /// <summary>提交自评（含各维度评分）</summary>
    [HttpPut("records/{id}/self-evaluate")]
    [RequirePermission(TaskPermissions.PerformanceView)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> SelfEvaluate(long id, [FromBody] SelfEvaluateRequest request)
    {
        return await _service.SelfEvaluateAsync(id, request, GetUserId());
    }

    /// <summary>上级评分/评语（含各维度评分+考核等级）</summary>
    [HttpPut("records/{id}/review")]
    [RequirePermission(TaskPermissions.PerformanceManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Review(long id, [FromBody] SuperiorReviewRequest request)
    {
        return await _service.ReviewAsync(id, request, GetUserId());
    }

    /// <summary>我的绩效（历史周期列表）</summary>
    [HttpGet("my")]
    [RequirePermission(TaskPermissions.PerformanceView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<PerformanceRecordListDto>>> GetMyPerformance()
    {
        return await _service.GetMyPerformanceAsync(GetUserId(), GetOrgId());
    }

    /// <summary>绩效看板（部门/团队统计）</summary>
    [HttpGet("dashboard")]
    [RequirePermission(TaskPermissions.PerformanceView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PerformanceDashboardDto>> GetDashboard([FromQuery] long? periodId)
    {
        return await _service.GetDashboardAsync(GetOrgId(), periodId);
    }

    // ===== 维度配置 =====

    /// <summary>获取评价维度配置列表</summary>
    [HttpGet("dimensions")]
    [RequirePermission(TaskPermissions.PerformanceView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<PerformanceDimensionListDto>>> GetDimensions()
    {
        return await _service.GetDimensionsAsync(GetOrgId());
    }

    /// <summary>创建评价维度</summary>
    [HttpPost("dimensions")]
    [RequirePermission(TaskPermissions.PerformanceManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<PerformanceDimensionListDto>> CreateDimension([FromBody] CreatePerformanceDimensionRequest request)
    {
        return await _service.CreateDimensionAsync(request, GetOrgId());
    }

    /// <summary>更新评价维度</summary>
    [HttpPut("dimensions/{id}")]
    [RequirePermission(TaskPermissions.PerformanceManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<PerformanceDimensionListDto>> UpdateDimension(long id, [FromBody] UpdatePerformanceDimensionRequest request)
    {
        return await _service.UpdateDimensionAsync(id, request);
    }

    /// <summary>删除评价维度</summary>
    [HttpDelete("dimensions/{id}")]
    [RequirePermission(TaskPermissions.PerformanceManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> DeleteDimension(long id)
    {
        return await _service.DeleteDimensionAsync(id);
    }
}
