using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinBankReconciliation : BaseEntity, ITenantScoped
{
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FAccountSetId { get; set; }
    public long FBankStatementId { get; set; }
    public long FVoucherId { get; set; }
    public long? FVoucherEntryId { get; set; }
    public string FMatchType { get; set; } = string.Empty;
    public DateTime FMatchTime { get; set; }
    public long FOperatorId { get; set; }
}
