using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 下载日志（迁移自 DcDownloadLog）
/// </summary>
public class CfDownloadLog : BaseEntity
{
    public long FTaskId { get; set; }
    public DateTime FStartTime { get; set; }
    public DateTime? FEndTime { get; set; }
    public int FStatus { get; set; }
    public int FDownloadFileCount { get; set; }
    public string? FFilePathList { get; set; }
    public string? FErrorMessage { get; set; }
}
