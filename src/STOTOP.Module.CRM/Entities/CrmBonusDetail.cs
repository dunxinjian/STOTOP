using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmBonusDetail : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FPlanId { get; set; }
    public long FEmployeeId { get; set; }
    public decimal FAmount { get; set; } = 0;
    public int FBonusType { get; set; }
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmBonusPlan Plan { get; set; } = null!;
}
