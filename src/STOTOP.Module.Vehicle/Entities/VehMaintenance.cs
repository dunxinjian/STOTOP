using STOTOP.Core.Models;

namespace STOTOP.Module.Vehicle.Entities;

public class VehMaintenance : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public long FVehicleId { get; set; }
    public DateTime FMaintenanceDate { get; set; }             // 维修日期
    public string? FMaintenanceType { get; set; }              // 维修类型
    public string FMaintenanceItem { get; set; } = string.Empty; // 维修项目
    public string? FMaintenanceUnit { get; set; }              // 维修单位
    public decimal? FMaintenanceCost { get; set; }             // 维修费用
    public int FCostBearer { get; set; } = 1;                  // 1=公司, 2=员工
    public DateTime? FCompletionDate { get; set; }             // 完成日期
    public int FMaintenanceStatus { get; set; } = 1;           // 1=维修中, 2=已完成
    public string? FRemark { get; set; }
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public VehVehicle Vehicle { get; set; } = null!;
}
