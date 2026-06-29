using STOTOP.Module.Express.Dtos;

namespace STOTOP.Module.Express.Services;

public interface IFlowAnalysisService
{
    Task<List<FlowAnalysisDto>> GetFlowDistributionAsync(ReportQueryRequest request);
    Task<List<FlowTrendDto>> GetFlowTrendAsync(ReportQueryRequest request, string granularity);
}
