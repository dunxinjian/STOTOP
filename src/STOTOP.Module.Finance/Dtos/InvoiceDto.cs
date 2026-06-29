using STOTOP.Core.Models;

using VoucherWordConst = STOTOP.Module.Finance.Constants.VoucherWord;

namespace STOTOP.Module.Finance.Dtos;

/// <summary>发票展示DTO</summary>
public class InvoiceDto
{
    public long Id { get; set; }
    public long AccountSetId { get; set; }
    public string InvoiceType { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string? InvoiceCode { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string? SellerName { get; set; }
    public string? SellerTaxNo { get; set; }
    public string? BuyerName { get; set; }
    public string? BuyerTaxNo { get; set; }
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TaxRate { get; set; }
    public string Direction { get; set; } = string.Empty;
    public int MatchStatus { get; set; }
    public long? MatchedVoucherId { get; set; }
    public long? ImportBatchId { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>导入请求</summary>
public class InvoiceImportRequest
{
    public long AccountSetId { get; set; }
}

/// <summary>查询请求</summary>
public class InvoiceQueryRequest : PagedRequest
{
    public string? InvoiceType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Direction { get; set; }
    public int? MatchStatus { get; set; }
}

/// <summary>匹配凭证请求</summary>
public class InvoiceMatchRequest
{
    public long VoucherId { get; set; }
}

/// <summary>进销项汇总DTO</summary>
public class TaxSummaryDto
{
    public int Month { get; set; }
    public decimal InputTaxAmount { get; set; }
    public decimal OutputTaxAmount { get; set; }
    public decimal TaxPayable { get; set; }
}

/// <summary>从发票生成凭证的请求</summary>
public class InvoiceGenerateVoucherRequest
{
    public long PeriodId { get; set; }
    public string VoucherWord { get; set; } = VoucherWordConst.Ji;
}

/// <summary>发票分页结果</summary>
public class InvoicePagedResult
{
    public List<InvoiceDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}
