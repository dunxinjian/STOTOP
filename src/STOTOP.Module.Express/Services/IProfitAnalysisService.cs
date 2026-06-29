using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IProfitAnalysisService
{
    Task<List<ProfitByClientDto>> GetProfitByClientAsync(ReportQueryRequest request);
    Task<List<ProfitByShopDto>> GetProfitByShopAsync(ReportQueryRequest request);
    Task<List<ProfitTrendDto>> GetProfitTrendAsync(ReportQueryRequest request, string granularity);
    Task<List<ProfitByIntermediaryDto>> GetProfitByIntermediaryAsync(ReportQueryRequest request);
    Task<List<ProfitBySalesmanDto>> GetProfitBySalesmanAsync(ReportQueryRequest request);
    Task<List<ProfitByWeightSegmentDto>> GetProfitByWeightSegmentAsync(ReportQueryRequest request);
    Task<List<ProfitByRegionDto>> GetProfitByRegionAsync(ReportQueryRequest request);
    Task<List<ProfitByProvinceDto>> GetProfitByProvinceAsync(ReportQueryRequest request, string? region);
    Task<ProfitFilterOptionsDto> GetFilterOptionsAsync();
}
