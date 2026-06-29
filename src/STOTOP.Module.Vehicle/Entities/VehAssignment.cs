using STOTOP.Core.Models;

namespace STOTOP.Module.Vehicle.Entities;

public class VehAssignment : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public long FVehicleId { get; set; }                       // 车辆ID
    public long FEmployeeId { get; set; }                      // 员工ID
    public string? FEmployeeName { get; set; }                 // 员工姓名
    public int FAssignmentType { get; set; }                   // 1=免费使用, 2=租赁
    public DateTime FStartDate { get; set; }                   // 开始日期
    public DateTime? FEndDate { get; set; }                    // 结束日期
    public int FAssignmentStatus { get; set; } = 1;            // 1=使用中, 2=已归还
    public string? FRemark { get; set; }
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public VehVehicle Vehicle { get; set; } = null!;
    public List<VehRentalCharge> RentalCharges { get; set; } = new();
}
