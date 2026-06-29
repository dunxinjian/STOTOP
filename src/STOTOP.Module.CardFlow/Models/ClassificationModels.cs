namespace STOTOP.Module.CardFlow.Models;

public class ClassificationResult
{
    public long BatchId { get; set; }
    public string TargetTable { get; set; } = string.Empty;
    public List<ClassificationItem> Items { get; set; } = new();
}

public class ClassificationItem
{
    public long DispatchRuleId { get; set; }          // 派发规则ID（核心字段）
    public string Type { get; set; } = string.Empty;  // 规则名称（显示用）
    public string Severity { get; set; } = "Info";
    public List<long> AffectedRowIds { get; set; } = new();
    public int AffectedRowCount { get; set; }
    public long RuleId { get; set; }                   // 保留兼容
    public string RuleName { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
}
