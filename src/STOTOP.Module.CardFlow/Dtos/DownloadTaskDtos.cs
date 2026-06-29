namespace STOTOP.Module.CardFlow.Dtos;

/// <summary>
/// 下载任务列表/详情返回 DTO
/// </summary>
public class DownloadTaskDto
{
    public long Id { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string? LoginAccount { get; set; }
    public string? StoragePath { get; set; }
    public string? CronExpression { get; set; }
    public string? HangfireJobId { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
}

/// <summary>
/// 下载任务详情 DTO（含脚本配置）
/// </summary>
public class DownloadTaskDetailDto : DownloadTaskDto
{
    public string? Password { get; set; }
    public string? ScriptConfig { get; set; }
    public string? FilterConfig { get; set; }
    public List<DownloadStepDto> Steps { get; set; } = new();
}

/// <summary>
/// 创建下载任务请求 DTO
/// </summary>
public class DownloadTaskCreateDto
{
    public string TaskName { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string? LoginAccount { get; set; }
    public string? LoginPassword { get; set; }
    public string? ScriptConfig { get; set; }
    public string? FilterConfig { get; set; }
    public string? StoragePath { get; set; }
    public string? CronExpression { get; set; }
    public int Status { get; set; } = 1;
    public List<DownloadStepCreateDto> Steps { get; set; } = new();
}

/// <summary>
/// 更新下载任务请求 DTO
/// </summary>
public class DownloadTaskUpdateDto : DownloadTaskCreateDto { }

/// <summary>
/// 下载步骤返回 DTO
/// </summary>
public class DownloadStepDto
{
    public long Id { get; set; }
    public int SortOrder { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? Selector { get; set; }
    public string? Value { get; set; }
    public int? WaitTime { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 创建下载步骤请求 DTO
/// </summary>
public class DownloadStepCreateDto
{
    public int SortOrder { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? Selector { get; set; }
    public string? Value { get; set; }
    public int? WaitTime { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 下载日志返回 DTO
/// </summary>
public class DownloadLogDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public string? TaskName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int Status { get; set; }
    public int DownloadFileCount { get; set; }
    public string? FilePathList { get; set; }
    public string? ErrorMessage { get; set; }
}
