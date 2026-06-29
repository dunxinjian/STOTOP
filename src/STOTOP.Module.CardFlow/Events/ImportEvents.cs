using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.CardFlow.Events;

public class ImportBatchCompletedEvent : BusinessEvent
{
    public long BatchId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailRows { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public long OperatorId { get; set; }
}

public class ImportBatchFailedEvent : BusinessEvent
{
    public long BatchId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public long OperatorId { get; set; }
}

public class ImportErrorDispatchedEvent : BusinessEvent
{
    public long BatchId { get; set; }
    public int ErrorCount { get; set; }
    public string DispatchType { get; set; } = string.Empty;
    public long AssigneeId { get; set; }
}

public class ClassificationCompletedEvent : BusinessEvent
{
    public long BatchId { get; set; }
    public string FileName { get; set; } = "";
    public string TargetTable { get; set; } = "";
    public int TotalClassifications { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> ClassificationTypes { get; set; } = new();
    public long OperatorId { get; set; }
}

/// <summary>批次已物理删除事件（用于审计和监控，非清理）</summary>
public class ImportBatchPurgedEvent : BusinessEvent
{
    public long BatchId { get; set; }
    public string? TargetTable { get; set; }
    public long OperatorId { get; set; }
    public DateTime PurgedAt { get; set; }
}

/// <summary>批次已撤销事件（软删除通知）</summary>
public class ImportBatchRevokedEvent : BusinessEvent
{
    public long BatchId { get; set; }
    public long OperatorId { get; set; }
    public DateTime RevokedAt { get; set; }
}
