namespace STOTOP.Module.CardFlow.Dtos;

// ===== IssueType DTOs =====

public class QualityIssueTypeDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Module { get; set; } = "Express";
    public string? SourceAutoPlugin { get; set; }
    public string SeverityLevel { get; set; } = "Warning";
    public string Category { get; set; } = "DataQuality";
    public bool IsBuiltIn { get; set; }
    public string? SuggestedFix { get; set; }
    public string? DetailRoute { get; set; }
    public string? DispatchMode { get; set; }
    public string? DispatchTarget { get; set; }
    public string? ResolveMode { get; set; }
    public string? CardFlowCode { get; set; }
    public string? CardTemplateCode { get; set; }
    public string? ActionSchemaJson { get; set; }
    public string? AfterResolvedAction { get; set; }
    public string? AggregationMode { get; set; }
    public bool OrgScoped { get; set; }
    public int TimeoutHours { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateQualityIssueTypeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Module { get; set; } = "Express";
    public string? SourceAutoPlugin { get; set; }
    public string SeverityLevel { get; set; } = "Warning";
    public string Category { get; set; } = "DataQuality";
    public string? SuggestedFix { get; set; }
    public string? DetailRoute { get; set; }
    public string? DispatchMode { get; set; }
    public string? DispatchTarget { get; set; }
    public string? ResolveMode { get; set; }
    public string? CardFlowCode { get; set; }
    public string? CardTemplateCode { get; set; }
    public string? ActionSchemaJson { get; set; }
    public string? AfterResolvedAction { get; set; }
    public string? AggregationMode { get; set; } = "BatchIssue";
    public bool OrgScoped { get; set; } = true;
    public int TimeoutHours { get; set; }
    public int Status { get; set; } = 1;
}

public class UpdateQualityIssueTypeRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Module { get; set; }
    public string? SourceAutoPlugin { get; set; }
    public string? SeverityLevel { get; set; }
    public string? Category { get; set; }
    public string? SuggestedFix { get; set; }
    public string? DetailRoute { get; set; }
    public string? DispatchMode { get; set; }
    public string? DispatchTarget { get; set; }
    public string? ResolveMode { get; set; }
    public string? CardFlowCode { get; set; }
    public string? CardTemplateCode { get; set; }
    public string? ActionSchemaJson { get; set; }
    public string? AfterResolvedAction { get; set; }
    public string? AggregationMode { get; set; }
    public bool? OrgScoped { get; set; }
    public int? TimeoutHours { get; set; }
    public int? Status { get; set; }
}

public class QualityIssueTypeListRequest
{
    public int? Status { get; set; }
    public string? Category { get; set; }
    public string? Module { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
