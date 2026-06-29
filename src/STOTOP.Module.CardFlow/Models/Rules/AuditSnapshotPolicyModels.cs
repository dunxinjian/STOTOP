namespace STOTOP.Module.CardFlow.Models.Rules;

public sealed class AuditSnapshotPolicy
{
    public Dictionary<string, string> FieldPolicies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class AuditSnapshotModes
{
    public const string Store = "store";
    public const string Mask = "mask";
    public const string NeverStore = "neverStore";
}
