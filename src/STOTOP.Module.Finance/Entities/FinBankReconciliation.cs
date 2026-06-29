using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinBankReconciliation : BaseEntity
{
    public long FAccountSetId { get; set; }
    public long FBankStatementId { get; set; }
    public long FVoucherId { get; set; }
    public long? FVoucherEntryId { get; set; }
    public string FMatchType { get; set; } = string.Empty;
    public DateTime FMatchTime { get; set; }
    public long FOperatorId { get; set; }
}
