using STOTOP.Core.Models;

namespace STOTOP.Module.Dormitory.Entities;

/// <summary>
/// 卫生检查
/// </summary>
public class DorHygieneCheck : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FRoomId { get; set; }
    public long FInspectorId { get; set; }
    public DateTime FCheckDate { get; set; }
    public int? FScore { get; set; }
    public string? FResult { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;

    // Navigation
    public DorRoom Room { get; set; } = null!;
}
