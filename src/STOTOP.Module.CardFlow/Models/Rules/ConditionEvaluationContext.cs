namespace STOTOP.Module.CardFlow.Models.Rules;

public sealed class ConditionEvaluationContext
{
    public Dictionary<string, object?> CardData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> DetailSummary { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> SourceContext { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> Initiator { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> InitiatorOrg { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> RoleCodes { get; set; } = new();
    public List<string> RoleNames { get; set; } = new();
    public List<string> OrgChain { get; set; } = new();
    public Dictionary<string, object?> Relations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> CurrentStageResult { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
