namespace STOTOP.Module.CardFlow.Dtos;

public class ProcessingIssueDto
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public long? StagingId { get; set; }
    public int RowNumber { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string? IssueName { get; set; }
    public string? SeverityLevel { get; set; }
    public string? ErrorField { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuggestedFix { get; set; }
    public string? OriginalValue { get; set; }
    public string? QualityDimension { get; set; }
    public string? DispatchStatus { get; set; }
    public string? DispatchType { get; set; }
    public long? DispatchRecordId { get; set; }
    public long? WorkItemId { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public int ProcessResult { get; set; }
    public string ResolutionStatus { get; set; } = "Pending";
    public string? ResolutionPayloadJson { get; set; }
    public long? ResolvedBy { get; set; }
    public DateTime? ResolvedTime { get; set; }
    public string? RetryStatus { get; set; }
    public string? RetryMessage { get; set; }
    public string? ResolveMode { get; set; }
    public string? DetailRoute { get; set; }
    public string? CardFlowCode { get; set; }
    public string? CardTemplateCode { get; set; }
    public string? ActionSchemaJson { get; set; }
    public string? AfterResolvedAction { get; set; }
    public string? AggregationMode { get; set; }
    public long OrgId { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class ProcessingIssueQueryRequest
{
    public long? BatchId { get; set; }
    public string? ErrorType { get; set; }
    public string? SeverityLevel { get; set; }
    public string? ResolutionStatus { get; set; }
    public string? DispatchStatus { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class ProcessingIssueReportRequest
{
    public long BatchId { get; set; }
    public long? StagingId { get; set; }
    public int RowNumber { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string? SeverityLevel { get; set; }
    public string? ErrorField { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuggestedFix { get; set; }
    public string? OriginalValue { get; set; }
    public string? QualityDimension { get; set; }
    public string? ResolutionPayloadJson { get; set; }
}

public class ProcessingIssueResolveRequest
{
    public string? PayloadJson { get; set; }
    public string? Message { get; set; }
}

public class ProcessingIssueRetryRequest
{
    public string? RetryAction { get; set; }
    public string? Message { get; set; }
    public string? PayloadJson { get; set; }
}

