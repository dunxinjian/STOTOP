using STOTOP.Core.Models;

namespace STOTOP.Module.Dormitory.Entities;

/// <summary>
/// 报修工单
/// </summary>
public class DorRepairOrder : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FRoomId { get; set; }
    public long FReporterId { get; set; }
    public string FDescription { get; set; } = string.Empty;
    public int FPriority { get; set; } = 1;
    public long? FHandlerId { get; set; }
    public string? FResult { get; set; }
    public DateTime? FHandledTime { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public DorRoom Room { get; set; } = null!;
}
