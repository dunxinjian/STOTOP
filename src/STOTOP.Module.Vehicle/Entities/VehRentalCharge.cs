using STOTOP.Core.Models;

namespace STOTOP.Module.Vehicle.Entities;

public class VehRentalCharge : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public long FVehicleId { get; set; }
    public long FAssignmentId { get; set; }
    public long FEmployeeId { get; set; }
    public string? FEmployeeName { get; set; }
    public long? FRentalStandardId { get; set; }
    public DateTime FChargePeriodStart { get; set; }           // 收费周期开始
    public DateTime FChargePeriodEnd { get; set; }             // 收费周期结束
    public decimal FAmountDue { get; set; }                    // 应收金额
    public decimal? FAmountPaid { get; set; }                  // 实收金额
    public int FChargeStatus { get; set; } = 1;                // 1=待收, 2=已收, 3=逾期, 4=减免
    public DateTime? FChargeDate { get; set; }                 // 实际收费日期
    public long? FVoucherId { get; set; }                      // 财务凭证ID
    public string? FRemark { get; set; }
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    // 导航属性
    public VehAssignment Assignment { get; set; } = null!;
}
