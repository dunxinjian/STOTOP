namespace STOTOP.Module.CardFlow.Models.Approval;

public sealed class ApproverStrategyConfig
{
    public List<ApproverRule> Rules { get; set; } = new();
    public ApproverFallbackPolicy? Fallback { get; set; }
}

public sealed class ApproverRule
{
    public string Strategy { get; set; } = "initiator";
    public Dictionary<string, object?> Config { get; set; } = new();
}

public sealed class ApproverConditionRule
{
    public string? ConditionJson { get; set; }
    public ApproverRule Rule { get; set; } = new();
}

public sealed class ApproverFallbackPolicy
{
    public string Type { get; set; } = "failSubmit";
    public Dictionary<string, object?> Config { get; set; } = new();
}

public sealed class ResolvedApprover
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Source { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ApproverResolveResult
{
    public List<ResolvedApprover> Approvers { get; set; } = new();
    public string? ApprovalModeOverride { get; set; }
    public string? FallbackReason { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Success => Approvers.Count > 0 && string.IsNullOrWhiteSpace(ErrorMessage);
}

public sealed class ApprovalModeConfig
{
    public string Mode { get; set; } = "single";
}
