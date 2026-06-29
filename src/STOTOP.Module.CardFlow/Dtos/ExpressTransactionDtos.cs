namespace STOTOP.Module.CardFlow.Dtos;

/// <summary>
/// 快递交易明细列表返回 DTO
/// </summary>
public class ExpressTransactionDto
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public string? BatchNo { get; set; }
    public string? SerialNumber { get; set; }
    public string? FileTypeName { get; set; }
    public DateTime? BusinessDate { get; set; }
    public string? OutletCode { get; set; }
    public string? OutletName { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessSummary { get; set; }
    public string? FeeName { get; set; }
    public decimal IncomeAmount { get; set; }
    public decimal ExpenseAmount { get; set; }
    public decimal Balance { get; set; }
    public string? FeeCategory { get; set; }
    public string? FeeAccountCode { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 快递交易明细详情 DTO（含全部字段）
/// </summary>
public class ExpressTransactionDetailDto : ExpressTransactionDto
{
    public DateTime? AccountingDate { get; set; }
    public string? ReportBusinessDate { get; set; }
    public string? ContactInfo { get; set; }
    public string? BankAccount { get; set; }
    public string? RelatedVoucherNo { get; set; }
    public string? VoucherType { get; set; }
    public string? RawDataJson { get; set; }
    public string? BusinessKey { get; set; }
}
