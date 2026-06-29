using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Rules;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IConditionEvaluationContextBuilder
{
    Task<ConditionEvaluationContext> BuildAsync(
        CfCard card,
        CfStageInstance? currentStage = null,
        CancellationToken cancellationToken = default);
}
