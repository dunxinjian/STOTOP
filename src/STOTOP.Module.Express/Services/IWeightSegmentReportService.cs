using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IWeightSegmentReportService
{
    Task<List<WeightSegmentReportDto>> GetWeightDistributionAsync(ReportQueryRequest request);
    Task<List<WeightTrendDto>> GetAvgWeightTrendAsync(ReportQueryRequest request, string granularity);
}
