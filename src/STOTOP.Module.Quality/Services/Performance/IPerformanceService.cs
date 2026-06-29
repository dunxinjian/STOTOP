using STOTOP.Core.Models;
using STOTOP.Module.Quality.Dtos;

namespace STOTOP.Module.Quality.Services.Performance;

public interface IPerformanceService
{
    /// <summary>绩效分页列表</summary>
    Task<ApiResult<PagedResult<PerformanceDto>>> GetPagedAsync(long orgId, PerformancePagedRequest request);
    /// <summary>我的绩效</summary>
    Task<ApiResult<PerformanceDto>> GetMyPerformanceAsync(long orgId, long userId, string period);
    /// <summary>绩效统计</summary>
    Task<ApiResult<PerformanceStatsDto>> GetStatsAsync(long orgId, long userId, string? period);
    /// <summary>绩效排名</summary>
    Task<ApiResult<PagedResult<PerformanceRankingDto>>> GetRankingAsync(long orgId, string? period, int pageIndex, int pageSize);
    /// <summary>绩效趋势</summary>
    Task<ApiResult<List<PerformanceTrendDto>>> GetTrendAsync(long orgId, long userId);
}
