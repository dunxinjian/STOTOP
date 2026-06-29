using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 下载步骤（迁移自 CfDownloadStep）
/// </summary>
public class CfDownloadStep : BaseEntity
{
    public long FTaskId { get; set; }
    public int FSortOrder { get; set; }
    public string FActionType { get; set; } = string.Empty;
    public string? FSelector { get; set; }
    public string? FValue { get; set; }
    public int? FWaitTime { get; set; }
    public string? FDescription { get; set; }
}
