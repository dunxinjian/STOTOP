using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmCustomerProfit : BaseEntity, IOrgScoped, ITenantScoped
{
    public string FCustomerId { get; set; } = string.Empty;
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string FPeriod { get; set; } = string.Empty;
    public decimal FRevenue { get; set; } = 0;
    public decimal FCost { get; set; } = 0;
    public decimal FProfit { get; set; } = 0;
    public decimal FProfitRate { get; set; } = 0;
    public int FDataSource { get; set; } = 1;
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmCustomer Customer { get; set; } = null!;
}
