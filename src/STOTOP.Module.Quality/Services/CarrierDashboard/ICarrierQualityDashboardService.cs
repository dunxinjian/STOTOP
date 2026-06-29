using STOTOP.Core.Models;
using STOTOP.Module.Quality.Dtos.CarrierDashboard;

namespace STOTOP.Module.Quality.Services.CarrierDashboard;

public interface ICarrierQualityDashboardService
{
    // 视图1 网点总览
    Task<ApiResult<NetworkKpiDto>> GetNetworkKpiAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode);
    Task<ApiResult<List<NetworkTrendPointDto>>> GetNetworkTrendAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode);
    Task<ApiResult<List<DomainStatItem>>> GetDomainDistributionAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode);
    Task<ApiResult<List<DomainStatItem>>> GetFeeByDomainAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode);
    Task<ApiResult<List<NetworkOptionDto>>> GetNetworkOptionsAsync(long orgId, string carrier);

    // 视图2 员工质量
    Task<ApiResult<EmployeeRankDto>> GetEmployeeRankAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode, string dimension, int topN);
    Task<ApiResult<EmployeeMetricsPageDto>> GetEmployeeMetricsAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode, int page, int size);
    Task<ApiResult<List<EmployeeEventItemDto>>> GetEmployeeTimelineAsync(long orgId, string carrier, string empNo, DateTime from, DateTime to);

    // 视图3 问题件追踪
    Task<ApiResult<EventPageDto>> GetEventsAsync(long orgId, EventQuery query);
    Task<ApiResult<int>> GetPendingCountAsync(long orgId, string carrier);
}
