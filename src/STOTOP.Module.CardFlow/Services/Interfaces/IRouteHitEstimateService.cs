using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IRouteHitEstimateService
{
    Task<RouteHitEstimateDto> EstimateAsync(
        long definitionId,
        RouteHitEstimateRequest request,
        CancellationToken cancellationToken = default);
}
