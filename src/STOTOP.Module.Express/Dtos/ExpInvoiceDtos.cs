using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 账单详情
/// </summary>
public class InvoiceDto
{
    public long Id { get; set; }
    public string? InvoiceNo { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public long? QuotationId { get; set; }
    public string? ClientType { get; set; }
    public string? BrandCode { get; set; }
    public string? BrandName { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int? TotalWaybills { get; set; }
    public decimal? TotalWeight { get; set; }
    public decimal? AvgWeight { get; set; }
    public decimal? WeightCap { get; set; }
    public decimal? ExcessWeight { get; set; }
    public decimal? WeightCapSurcharge { get; set; }
    public decimal? QuotaSurcharge { get; set; }
    public decimal? TotalCharge { get; set; }
    public decimal? TotalChargeWithSurcharge { get; set; }
    public decimal? TotalCost { get; set; }
    public decimal? TotalProfit { get; set; }
    public decimal? PrepayDeduction { get; set; }
    public decimal? PayableAmount { get; set; }
    public int ReviewStatus { get; set; }
    public string? Reviewer { get; set; }
    public DateTime? ReviewTime { get; set; }
    public string? ReviewRemark { get; set; }
    public int Status { get; set; }
    public bool Archived { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 账单详情（含运单汇总）
/// </summary>
public class InvoiceDetailDto : InvoiceDto
{
    public List<InvoiceReviewLogDto> ReviewLogs { get; set; } = new();
}

/// <summary>
/// 审核日志DTO
/// </summary>
public class InvoiceReviewLogDto
{
    public long Id { get; set; }
    public int Action { get; set; }
    public long? RuleId { get; set; }
    public string? RuleResult { get; set; }
    public long? OperatorId { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 账单查询请求
/// </summary>
public class InvoiceQueryRequest : PagedRequest
{
    public string? ClientId { get; set; }
    public string? BrandCode { get; set; }
    public int? Status { get; set; }
    public int? ReviewStatus { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
}

/// <summary>
/// 生成账单请求
/// </summary>
public class GenerateInvoiceRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}

/// <summary>
/// 人工审核请求
/// </summary>
public class ReviewInvoiceRequest
{
    public bool Approved { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 收款请求
/// </summary>
public class ReceivePaymentRequest
{
    public decimal Amount { get; set; }
}

/// <summary>
/// 反审核请求
/// </summary>
public class ReverseReviewRequest
{
    public string? Remark { get; set; }
}

/// <summary>对账明细行</summary>
public class ReconciliationLineDto
{
    public long WaybillId { get; set; }
    public string WaybillNo { get; set; } = "";
    public DateTime WaybillDate { get; set; }
    public string ProvinceName { get; set; } = "";
    public decimal BillableWeight { get; set; }
    public decimal FreightCharge { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal ChargeAmount { get; set; }
}

/// <summary>对账详情</summary>
public class ReconciliationDetailDto
{
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = "";
    public string ClientName { get; set; } = "";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int ReconciliationStatus { get; set; }
    public string? Remarks { get; set; }
    public string? DisputeReason { get; set; }
    public string? DisputeResolution { get; set; }
    public decimal TotalCharge { get; set; }
    public int TotalWaybills { get; set; }
    public List<ReconciliationLineDto> Lines { get; set; } = new();
}

/// <summary>确认对账请求</summary>
public class ReconciliationConfirmRequest
{
    public string? Remarks { get; set; }
}

/// <summary>提起异议请求</summary>
public class ReconciliationDisputeRequest
{
    public string Reason { get; set; } = "";
}

/// <summary>处理异议请求</summary>
public class ReconciliationResolveRequest
{
    public string Resolution { get; set; } = "";
}
