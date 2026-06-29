using STOTOP.Core.Models;

namespace STOTOP.Module.Vehicle.Entities;

public class VehInsurance : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public long FVehicleId { get; set; }                       // 车辆ID
    public string FInsuranceType { get; set; } = string.Empty; // 保险类型
    public string? FInsuranceCompany { get; set; }             // 保险公司
    public string? FPolicyNo { get; set; }                     // 保单号
    public decimal? FPremium { get; set; }                     // 保费
    public DateTime FEffectiveDate { get; set; }               // 生效日期
    public DateTime FExpiryDate { get; set; }                  // 到期日期
    public int FInsuranceStatus { get; set; } = 1;             // 保险状态
    public string? FRemark { get; set; }                       // 备注
    public long? FCreatorId { get; set; }                      // 创建人ID
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
