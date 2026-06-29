using STOTOP.Core.Models;
using STOTOP.Module.Quality.Dtos;

namespace STOTOP.Module.Quality.Services.Dashboard;

public interface IQualityDashboardService
{
    /// <summary>获取看板统计数据</summary>
    Task<ApiResult<DashboardStatsDto>> GetStatsAsync(long orgId);

    /// <summary>获取最近异常列表</summary>
    Task<ApiResult<List<ExceptionListDto>>> GetRecentExceptionsAsync(long orgId, int count);

    /// <summary>获取趋势数据</summary>
    Task<ApiResult<List<TrendDataPoint>>> GetTrendDataAsync(long orgId, int days);

    /// <summary>获取类型分布</summary>
    Task<ApiResult<List<DistributionItem>>> GetTypeDistributionAsync(long orgId);

    /// <summary>获取优先级分布</summary>
    Task<ApiResult<List<DistributionItem>>> GetPriorityDistributionAsync(long orgId);

    /// <summary>异常趋势分析</summary>
    Task<ApiResult<List<TrendAnalysisDto>>> GetTrendAnalysisAsync(long orgId, DateTime? startDate, DateTime? endDate, string groupBy);

    /// <summary>处理效率分析</summary>
    Task<ApiResult<EfficiencyAnalysisDto>> GetEfficiencyAnalysisAsync(long orgId, DateTime? startDate, DateTime? endDate);

    /// <summary>来源分布分析</summary>
    Task<ApiResult<List<SourceDistributionDto>>> GetSourceDistributionAsync(long orgId, DateTime? startDate, DateTime? endDate);

    /// <summary>处理人排名统计</summary>
    Task<ApiResult<List<HandlerStatsDto>>> GetHandlerStatsAsync(long orgId, DateTime? startDate, DateTime? endDate, int top);
}
