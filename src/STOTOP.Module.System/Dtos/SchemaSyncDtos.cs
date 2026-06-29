namespace STOTOP.Module.System.Dtos;

// === 数据库迁移管理 DTOs ===

public class SchemaSyncStatusDto
{
    public bool HasPendingChanges { get; set; }
    public int PendingCount { get; set; }
    public string SeederStatus { get; set; } = "";
    public string? LastSyncTime { get; set; }
}

public class SchemaChangeItemDto
{
    public long Id { get; set; }
    public string TableName { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public string ChangeType { get; set; } = "";
    public string SqlStatement { get; set; } = "";
    public string DetectedAt { get; set; } = "";
}

public class SchemaWarningItemDto
{
    public string TableName { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public string Message { get; set; } = "";
}

public class MigrationHistoryItemDto
{
    public long Id { get; set; }
    public string Module { get; set; } = "";
    public int Version { get; set; }
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public string ExecutedTime { get; set; } = "";
    public long? DurationMs { get; set; }
}

public class ExecuteChangesRequest
{
    public List<long> ChangeIds { get; set; } = new();
}

public class SkipChangesRequest
{
    public List<long> ChangeIds { get; set; } = new();
}
