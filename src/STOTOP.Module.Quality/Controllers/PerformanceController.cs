using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Services.Performance;

namespace STOTOP.Module.Quality.Controllers;

[Authorize]
[ApiController]
[Route("api/quality")]
public class PerformanceController : ControllerBase
{
    private readonly IPerformanceService _service;

    public PerformanceController(IPerformanceService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>绩效分页列表</summary>
    [HttpGet("performance")]
    [RequirePermission(QualityPermissions.PerformanceView)]
    public async Task<ApiResult<PagedResult<PerformanceDto>>> GetPaged([FromQuery] PerformancePagedRequest request)
    {
        return await _service.GetPagedAsync(GetOrgId(), request);
    }

    /// <summary>我的绩效</summary>
    [HttpGet("performance/my")]
    [RequirePermission(QualityPermissions.PerformanceView)]
    public async Task<ApiResult<PerformanceDto>> GetMy([FromQuery] string period)
    {
        return await _service.GetMyPerformanceAsync(GetOrgId(), GetUserId(), period);
    }

    /// <summary>绩效统计</summary>
    [HttpGet("performance/stats")]
    [RequirePermission(QualityPermissions.PerformanceView)]
    public async Task<ApiResult<PerformanceStatsDto>> GetStats([FromQuery] string? period)
    {
        return await _service.GetStatsAsync(GetOrgId(), GetUserId(), period);
    }

    /// <summary>绩效排名</summary>
    [HttpGet("performance/ranking")]
    [RequirePermission(QualityPermissions.PerformanceView)]
    public async Task<ApiResult<PagedResult<PerformanceRankingDto>>> GetRanking([FromQuery] string? period, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20)
    {
        return await _service.GetRankingAsync(GetOrgId(), period, pageIndex, pageSize);
    }

    /// <summary>绩效趋势</summary>
    [HttpGet("performance/trend")]
    [RequirePermission(QualityPermissions.PerformanceView)]
    public async Task<ApiResult<List<PerformanceTrendDto>>> GetTrend([FromQuery] long? userId)
    {
        var targetUserId = userId ?? GetUserId();
        return await _service.GetTrendAsync(GetOrgId(), targetUserId);
    }
}
