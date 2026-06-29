using STOTOP.Module.CardFlow.Models.Rules;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IAuditSnapshotPolicyService
{
    string BuildRouteDecisionSnapshotJson(
        ConditionEvaluationContext context,
        StageRouteResolveResult routeResult);
}
