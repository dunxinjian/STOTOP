namespace STOTOP.Module.Workflow.DTOs;

public class IssuePackDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string? ChainId { get; set; }
    public long? WorkItemId { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public long? BatchId { get; set; }
    public int TotalCount { get; set; }
    public int ResolvedCount { get; set; }
    public string? Summary { get; set; }
    public DateTime CreateTime { get; set; }
    public List<IssueDetailDto> Details { get; set; } = new();
}

public class IssueDetailDto
{
    public long Id { get; set; }
    public long? RowId { get; set; }
    public string? TableName { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? FieldName { get; set; }
    public string? OriginalValue { get; set; }
    public string? CorrectedValue { get; set; }
    public bool IsResolved { get; set; }
    public long? ResolverId { get; set; }
    public DateTime? ResolvedTime { get; set; }
}

public class AggregateIssuesRequest
{
    public long OrgId { get; set; }
    public string? ChainId { get; set; }
    public string? DataScopeId { get; set; }
    public long? BatchId { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public List<IssueItem> Issues { get; set; } = new();

    // 是否自动创建 WorkItem（聚合后是否自动派发）
    public bool AutoCreateWorkItem { get; set; } = true;
    public long? CreatorId { get; set; }
}

public class IssueItem
{
    public long? RowId { get; set; }
    public string? TableName { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? FieldName { get; set; }
    public string? OriginalValue { get; set; }
}
