using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Models.Rules;

public sealed class StageRouteResolveResult
{
    public bool RuleMode { get; set; }
    public string FromStageKey { get; set; } = string.Empty;
    public CfStageRouteRule? SelectedRoute { get; set; }
    public CfStageDefinition? NextStage { get; set; }
    public List<StageRouteCandidateResult> Candidates { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public string CandidateResultsJson { get; set; } = "[]";
    public string DecisionSnapshotJson { get; set; } = "{}";

    public static StageRouteResolveResult LegacyFallback(string reason)
        => new() { RuleMode = false, Reason = reason };
}

public sealed class StageRouteCandidateResult
{
    public long? RouteRuleId { get; set; }
    public string EdgeKey { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public string ToStageKey { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsDefault { get; set; }
    public bool Matched { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public List<string> TypeErrors { get; set; } = new();
    public List<string> ConsumedFields { get; set; } = new();
}
