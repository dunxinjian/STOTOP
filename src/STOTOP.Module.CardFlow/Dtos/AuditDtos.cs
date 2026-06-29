namespace STOTOP.Module.CardFlow.Dtos;

public class CardFlowRuntimeAuditDto
{
    public long Id { get; set; }
    public string SnapshotType { get; set; } = string.Empty;
    public long CardId { get; set; }
    public int Round { get; set; }
    public long? StageInstanceId { get; set; }
    public string? StageName { get; set; }
    public string? StageKey { get; set; }
    public string? EdgeKey { get; set; }
    public string? PolicyKey { get; set; }
    public string? Title { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = new();
}

public class CardFlowRuntimeMonitoringRequest
{
    public long? OrgId { get; set; }
    public long? FlowDefinitionId { get; set; }
    public long? FlowVersionId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class CardFlowRuntimeMonitoringDto
{
    public int RouteHitCount { get; set; }
    public int FallbackCount { get; set; }
    public int HandlerUnresolvedCount { get; set; }
    public int DynamicInsertCount { get; set; }
    public int RuleWarningCount { get; set; }
    public List<CardFlowRuntimeMonitoringBucketDto> Buckets { get; set; } = new();
}

public class CardFlowRuntimeMonitoringBucketDto
{
    public string FlowCode { get; set; } = string.Empty;
    public long FlowVersionId { get; set; }
    public string? StageKey { get; set; }
    public string? EdgeKey { get; set; }
    public string? PolicyKey { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceType { get; set; }
    public long OrgId { get; set; }
    public string DateBucket { get; set; } = string.Empty;
    public int RouteHitCount { get; set; }
    public int FallbackCount { get; set; }
    public int HandlerUnresolvedCount { get; set; }
    public int DynamicInsertCount { get; set; }
    public int RuleWarningCount { get; set; }
}

public class VoucherTraceDto
{
    public long VoucherId { get; set; }
    public long? BatchId { get; set; }
    public string? BatchNo { get; set; }
    public string? FileName { get; set; }
    public int? RowNumber { get; set; }
    public string? RawDataJson { get; set; }
    public string? Operator { get; set; }
    public DateTime? ImportTime { get; set; }
    public string? UploadMethod { get; set; }
}

public class BatchAuditDto
{
    public long BatchId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? FileHash { get; set; }
    public long FileSize { get; set; }
    public string? Operator { get; set; }
    public string? UploadMethod { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? ImportStartTime { get; set; }
    public DateTime? ImportEndTime { get; set; }
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailRows { get; set; }
}
