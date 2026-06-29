using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

public interface IApproverResolver
{
    global::System.Threading.Tasks.Task<ApproverResolveResult> ResolveAsync(
        CfStageDefinition stageDefinition,
        CfCard card,
        IReadOnlyDictionary<string, object?> cardData,
        long flowOrgId,
        long initiatorId,
        string? flowSettingsJson = null,
        CancellationToken cancellationToken = default);
}
