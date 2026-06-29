using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinBankStatement : BaseEntity
{
    public long FAccountSetId { get; set; }
    public string? FBankAccount { get; set; }
    public string? FBankName { get; set; }
    public DateTime FTransactionDate { get; set; }
    public string? FDescription { get; set; }
    public decimal FDebitAmount { get; set; }
    public decimal FCreditAmount { get; set; }
    public decimal FBalance { get; set; }
    public string? FCounterparty { get; set; }
    public string? FReferenceNo { get; set; }
    public int FMatchStatus { get; set; }
    public long? FMatchedVoucherId { get; set; }
    public long? FImportBatchId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
