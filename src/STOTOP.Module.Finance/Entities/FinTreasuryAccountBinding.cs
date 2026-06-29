using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinTreasuryAccountBinding : BaseEntity
{
    public long FAccountSetId { get; set; }
    public long? FOrgId { get; set; }
    public long? FChannelId { get; set; }
    public long? FCashAccountId { get; set; }
    public string? FAccountNo { get; set; }
    public string FOpeningSource { get; set; } = "account_balance";
    public decimal? FManualOpeningAmount { get; set; }
    public int FStatus { get; set; } = 1;
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
