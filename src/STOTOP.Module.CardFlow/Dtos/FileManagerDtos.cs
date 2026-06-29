namespace STOTOP.Module.CardFlow.Dtos;

public class UploadedFileDto
{
    public long BatchId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadTime { get; set; }
    public string? SourceType { get; set; }
    public string? BatchNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public bool PhysicalFileExists { get; set; }
}

public class FileQueryFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SourceType { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; } = true;
}

public class StorageStatsDto
{
    public long TotalSize { get; set; }
    public int TotalFileCount { get; set; }
    public List<MonthlyStorageDto> MonthlyStats { get; set; } = new();
}

public class MonthlyStorageDto
{
    public string Month { get; set; } = string.Empty;
    public long Size { get; set; }
    public int FileCount { get; set; }
}

public class CleanupPolicyDto
{
    public long Id { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime? LastExecuteTime { get; set; }
    public DateTime CreateTime { get; set; }
}

public class SaveCleanupPolicyRequest
{
    public long? Id { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public int Status { get; set; }
}

public class CleanupResultDto
{
    public int DeletedFileCount { get; set; }
    public long FreedSpace { get; set; }
}

public class CleanupPreviewDto
{
    public int WillDeleteCount { get; set; }
    public long WillFreeSpace { get; set; }
    public List<string> FileNames { get; set; } = new();
}
