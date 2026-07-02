using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAuxiliaryBalance : BaseEntity, IAccountSetScoped, ITenantScoped
{
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FPeriodId { get; set; }
    public long FAccountId { get; set; }
    public string FAuxiliaryJson { get; set; } = string.Empty;
    public decimal FBeginDebit { get; set; }
    public decimal FBeginCredit { get; set; }
    public decimal FCurrentDebit { get; set; }
    public decimal FCurrentCredit { get; set; }
    public decimal FEndDebit { get; set; }
    public decimal FEndCredit { get; set; }
    public long FAccountSetId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
