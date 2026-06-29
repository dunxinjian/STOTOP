using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinInvoice : BaseEntity
{
    public long FAccountSetId { get; set; }
    public string FInvoiceType { get; set; } = string.Empty;
    public string FInvoiceNo { get; set; } = string.Empty;
    public string? FInvoiceCode { get; set; }
    public DateTime FInvoiceDate { get; set; }
    public string? FSellerName { get; set; }
    public string? FSellerTaxNo { get; set; }
    public string? FBuyerName { get; set; }
    public string? FBuyerTaxNo { get; set; }
    public decimal FAmount { get; set; }
    public decimal FTaxAmount { get; set; }
    public decimal FTotalAmount { get; set; }
    public decimal FTaxRate { get; set; }
    public string FDirection { get; set; } = string.Empty;
    public int FMatchStatus { get; set; }
    public long? FMatchedVoucherId { get; set; }
    public long? FImportBatchId { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
