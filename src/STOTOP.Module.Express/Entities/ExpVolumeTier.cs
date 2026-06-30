using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

public class ExpVolumeTier : BaseEntity, IOrgScoped, ITenantScoped
{
    public string FBusinessObjectId { get; set; } = string.Empty;
    public int FMinMonthlyVolume { get; set; }
    public long FQuotationPlanId { get; set; }
    public bool FIsActive { get; set; } = true;
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string FBrandCode { get; set; } = string.Empty;
}
