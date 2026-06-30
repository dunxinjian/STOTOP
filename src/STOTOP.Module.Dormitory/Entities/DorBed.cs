using STOTOP.Core.Models;

namespace STOTOP.Module.Dormitory.Entities;

public class DorBed : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FRoomId { get; set; }
    public string FBedNumber { get; set; } = string.Empty;
    public string FBedType { get; set; } = "lower";
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public DorRoom Room { get; set; } = null!;
}
