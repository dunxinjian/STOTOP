using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Rules;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IStageRouteResolver
{
    Task<StageRouteResolveResult> ResolveNextStageAsync(
        CfCard card,
        CfStageInstance currentStage,
        CancellationToken cancellationToken = default);
}
