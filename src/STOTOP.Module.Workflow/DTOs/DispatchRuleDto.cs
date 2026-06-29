namespace STOTOP.Module.Workflow.DTOs;

public class DispatchRuleDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Module { get; set; }
    public string? BizType { get; set; }
    public int DispatchMode { get; set; }
    public string? AutoAssignRule { get; set; }
    public int Timeout { get; set; }
    public string? EscalationRule { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
}

public class DispatchResult
{
    public bool Success { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string DispatchMode { get; set; } = string.Empty;  // Auto/Hybrid/Manual
    public string? Message { get; set; }
}

public class RevokeResultDto
{
    public bool Success { get; set; }
    public int TotalAffectedRows { get; set; }
    public List<RevokeLogDto> Logs { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class RevokeLogDto
{
    public long Id { get; set; }
    public string? ChainId { get; set; }
    public string RevokeType { get; set; } = string.Empty;
    public string? TargetTable { get; set; }
    public int AffectedRows { get; set; }
    public string? Strategy { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime CreateTime { get; set; }
}

public class CallbackContext
{
    public long WorkItemId { get; set; }
    public string? Module { get; set; }
    public string? BizType { get; set; }
    public long? BizId { get; set; }
    public string? ChainId { get; set; }
    public string? DataScopeId { get; set; }
    public string? Result { get; set; }
}
