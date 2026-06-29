using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 文件清理策略（迁移自 DcFileCleanupPolicy）
/// </summary>
public class CfFileCleanupPolicy : BaseEntity
{
    public string FPolicyName { get; set; } = string.Empty;
    public int FRetentionDays { get; set; }
    public string FCronExpression { get; set; } = string.Empty;
    public string? FHangfireJobId { get; set; }
    public int FStatus { get; set; }
    public DateTime? FLastExecutedTime { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
