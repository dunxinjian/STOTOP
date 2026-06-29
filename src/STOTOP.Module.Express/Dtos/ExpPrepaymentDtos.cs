using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 预付款记录DTO
/// </summary>
public class PrepaymentDto
{
    public long Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 预付款余额DTO
/// </summary>
public class PrepaymentBalanceDto
{
    public long Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal TotalRecharge { get; set; }
    public decimal TotalConsume { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 预付款流水DTO
/// </summary>
public class PrepaymentTransactionDto
{
    public long Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public int TransactionType { get; set; }
    public decimal Amount { get; set; }
    public long? InvoiceId { get; set; }
    public decimal? BalanceAfter { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 充值请求
/// </summary>
public class RechargeRequest
{
    public string ClientId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 流水查询请求
/// </summary>
public class TransactionQueryRequest : PagedRequest
{
    public string? ClientId { get; set; }
    public int? TransactionType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
