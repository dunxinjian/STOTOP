using STOTOP.Core.Models;

namespace STOTOP.Module.Vehicle.Entities;

public class VehRentalStandard : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public string FName { get; set; } = string.Empty;          // 费用标准名称
    public decimal FAmount { get; set; }                       // 金额
    public int FChargeCycle { get; set; }                      // 1=月, 2=季, 3=年
    public DateTime FEffectiveDate { get; set; }               // 生效日期
    public DateTime? FExpiryDate { get; set; }                 // 失效日期
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 1;
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
