using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 下载任务（迁移自 DcDownloadTask）
/// </summary>
public class CfDownloadTask : BaseEntity
{
    public string FTaskName { get; set; } = string.Empty;
    public string FTargetUrl { get; set; } = string.Empty;
    public string? FLoginAccount { get; set; }
    public string? FLoginPassword { get; set; }
    public string? FScriptConfig { get; set; }
    public string? FFilterConfig { get; set; }
    public string? FStoragePath { get; set; }
    public string? FCronExpression { get; set; }
    public string? FHangfireJobId { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime? FUpdatedTime { get; set; }
}
