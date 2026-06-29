using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysChangeLog : BaseEntity
{
    public string FBusinessType { get; set; } = string.Empty;
    public long FBusinessId { get; set; }
    public string FBusinessName { get; set; } = string.Empty;
    public string FOperationType { get; set; } = string.Empty;
    public string FChangeContent { get; set; } = string.Empty;
    public long? FOperatorId { get; set; }
    public string? FOperatorName { get; set; }
    public DateTime FOperationTime { get; set; } = DateTime.Now;
    public int FDingTalkSyncStatus { get; set; }
    public DateTime? FDingTalkSyncTime { get; set; }
    public string? FDingTalkSyncResult { get; set; }
    public string? FRemark { get; set; }
}
