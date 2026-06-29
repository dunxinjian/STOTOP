using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Services.Dashboard;

namespace STOTOP.Module.Quality.Controllers;

[Authorize]
[ApiController]
[Route("api/quality")]
public class QualityDashboardController : ControllerBase
{
    private readonly IQualityDashboardService _service;

    public QualityDashboardController(IQualityDashboardService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>获取看板统计数据</summary>
    [HttpGet("dashboard/stats")]
    [RequirePermission(QualityPermissions.DashboardView)]
    public async Task<ApiResult<DashboardStatsDto>> GetStats()
    {
        return await _service.GetStatsAsync(GetOrgId());
    }

    /// <summary>获取最近异常列表</summary>
    [HttpGet("dashboard/recent")]
    [RequirePermission(QualityPermissions.DashboardView)]
    public async Task<ApiResult<List<ExceptionListDto>>> GetRecentExceptions([FromQuery] int count = 10)
    {
        return await _service.GetRecentExceptionsAsync(GetOrgId(), count);
    }

    /// <summary>获取趋势数据</summary>
    [HttpGet("dashboard/trend")]
    [RequirePermission(QualityPermissions.DashboardView)]
    public async Task<ApiResult<List<TrendDataPoint>>> GetTrend([FromQuery] int days = 30)
    {
        return await _service.GetTrendDataAsync(GetOrgId(), days);
    }

    /// <summary>获取类型分布</summary>
    [HttpGet("dashboard/type-dist")]
    [RequirePermission(QualityPermissions.DashboardView)]
    public async Task<ApiResult<List<DistributionItem>>> GetTypeDistribution()
    {
        return await _service.GetTypeDistributionAsync(GetOrgId());
    }

    /// <summary>获取优先级分布</summary>
    [HttpGet("dashboard/priority-dist")]
    [RequirePermission(QualityPermissions.DashboardView)]
    public async Task<ApiResult<List<DistributionItem>>> GetPriorityDistribution()
    {
        return await _service.GetPriorityDistributionAsync(GetOrgId());
    }

    /// <summary>异常趋势分析</summary>
    [HttpGet("dashboard/analysis/trend")]
    [RequirePermission(QualityPermissions.DashboardView)]
    public async Task<ApiResult<List<TrendAnalysisDto>>> GetTrendAnalysis(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string groupBy = "day")
    {
        return await _service.GetTrendAnalysisAsync(GetOrgId(), startDate, endDate, groupBy);
    }

    /// <summary>处理效率分析</summary>
    [HttpGet("dashboard/analysis/efficiency")]
    [RequirePermission(QualityPermissions.DashboardView)]
    public async Task<ApiResult<EfficiencyAnalysisDto>> GetEfficiencyAnalysis(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        return await _service.GetEfficiencyAnalysisAsync(GetOrgId(), startDate, endDate);
    }

    /// <summary>来源分布分析</summary>
    [HttpGet("dashboard/analysis/source")]
    [RequirePermission(QualityPermissions.DashboardView)]
    public async Task<ApiResult<List<SourceDistributionDto>>> GetSourceDistribution(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        return await _service.GetSourceDistributionAsync(GetOrgId(), startDate, endDate);
    }

    /// <summary>处理人排名统计</summary>
    [HttpGet("dashboard/analysis/handler-stats")]
    [RequirePermission(QualityPermissions.DashboardView)]
    public async Task<ApiResult<List<HandlerStatsDto>>> GetHandlerStats(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int top = 10)
    {
        return await _service.GetHandlerStatsAsync(GetOrgId(), startDate, endDate, top);
    }
}
