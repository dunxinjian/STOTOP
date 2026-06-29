using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Rules;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IDynamicStagePolicyResolver
{
    Task<DynamicStagePolicyResolveResult> ResolveAsync(
        CfCard card,
        CfStageInstance sourceStage,
        string triggerTiming,
        StageRouteResolveResult? routeResult = null,
        CancellationToken cancellationToken = default);

    Task<DynamicStagePolicyResolveResult> ResolveBeforeTargetAsync(
        CfCard card,
        CfStageInstance sourceStage,
        StageRouteResolveResult routeResult,
        CancellationToken cancellationToken = default);
}
