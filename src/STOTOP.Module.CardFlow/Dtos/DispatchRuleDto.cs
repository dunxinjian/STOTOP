namespace STOTOP.Module.CardFlow.Dtos;

public class DispatchRuleCreateDto
{
    public string RuleName { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = "PipelineCompleted";
    public List<string>? TargetTables { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public object? Condition { get; set; }
    public string Severity { get; set; } = "Info";
    public string HandlerType { get; set; } = string.Empty;
    public object? HandlerConfig { get; set; }
    public bool IsAsync { get; set; } = true;
    public int Priority { get; set; } = 100;
    public string? Description { get; set; }
}

public class DispatchRuleUpdateDto
{
    public string? RuleName { get; set; }
    public string? TriggerEvent { get; set; }
    public List<string>? TargetTables { get; set; }
    public string? RuleType { get; set; }
    public object? Condition { get; set; }
    public string? Severity { get; set; }
    public string? HandlerType { get; set; }
    public object? HandlerConfig { get; set; }
    public bool? IsAsync { get; set; }
    public int? Priority { get; set; }
    public int? Status { get; set; }
    public string? Description { get; set; }
}

public class DispatchRuleResponseDto
{
    public long Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
    public List<string>? TargetTables { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public object? Condition { get; set; }
    public string Severity { get; set; } = "Info";
    public string HandlerType { get; set; } = string.Empty;
    public object? HandlerConfig { get; set; }
    public bool IsAsync { get; set; }
    public int Priority { get; set; }
    public int Status { get; set; }
    public string? Description { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
}

public class HandlerTypeInfo
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
