using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;

namespace STOTOP.Module.CardFlow.Models.Rules;

public sealed class DynamicStagePolicyResolveResult
{
    public bool ShouldInsert { get; set; }
    public CfDynamicStagePolicy? Policy { get; set; }
    public List<ResolvedApprover> Approvers { get; set; } = new();
    public string StageName { get; set; } = string.Empty;
    public string ApprovalMode { get; set; } = "single";
    public string TriggerTiming { get; set; } = string.Empty;
    public string InsertPosition { get; set; } = string.Empty;
    public bool ShouldReplaceTargetHandlers { get; set; }
    public string ContinuationStageKey { get; set; } = string.Empty;
    public string InsertContextJson { get; set; } = "{}";
    public string Reason { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public static DynamicStagePolicyResolveResult NoInsert(string reason)
        => new() { ShouldInsert = false, Reason = reason };
}
