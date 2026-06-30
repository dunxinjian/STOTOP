using STOTOP.Core.Models;

namespace STOTOP.Module.Dormitory.Entities;

public class DorRoom : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FBuildingId { get; set; }
    public int FFloor { get; set; }
    public string FRoomNumber { get; set; } = string.Empty;
    public int FBedsCount { get; set; } = 4;
    public string? FRoomType { get; set; }
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public DorBuilding Building { get; set; } = null!;
    public List<DorBed> Beds { get; set; } = new();
}
