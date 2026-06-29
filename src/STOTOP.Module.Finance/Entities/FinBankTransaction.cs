using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

/// <summary>
/// FIN银行流水
/// </summary>
public class FinBankTransaction : BaseEntity
{
    public long FChannelId { get; set; }
    public DateTime FTransactionDate { get; set; }
    public string FTransactionNo { get; set; } = string.Empty;
    public string? FCounterpartAccount { get; set; }
    public string? FCounterpartName { get; set; }
    public int FDirection { get; set; }
    public decimal FAmount { get; set; }
    public decimal? FBalance { get; set; }
    public string? FSummary { get; set; }
    public string? FRemark { get; set; }
    public long? FImportBatchId { get; set; }
    public int FMatchStatus { get; set; }
    public string? FRelatedBusinessType { get; set; }
    public long? FRelatedBusinessId { get; set; }
    public long? FVoucherId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; }
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }
}
