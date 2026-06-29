using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public sealed class StageActionPolicyService : IStageActionPolicyService
{
    public StageActionPolicyValidationResult ValidateAction(StageConfigEnvelope normalizedConfig, string action)
    {
        if (normalizedConfig.ActionPolicy?.AllowedActions is not { Count: > 0 } allowedActions)
        {
            return StageActionPolicyValidationResult.Ok();
        }

        return allowedActions.Contains(action, StringComparer.OrdinalIgnoreCase)
            ? StageActionPolicyValidationResult.Ok()
            : StageActionPolicyValidationResult.Fail($"当前节点不允许执行操作: {action}");
    }
}
