namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 收入详情DTO
/// </summary>
public class IncomeDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public long? AttendeeId { get; set; }
    public string? AttendeeName { get; set; }
    public string? Type { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PayerName { get; set; }
    public string? PayerOrganization { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Remark { get; set; }
    public string? Registrant { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 收入列表项DTO
/// </summary>
public class IncomeListItemDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public long? AttendeeId { get; set; }
    public string? AttendeeName { get; set; }
    public string? Type { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PayerName { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Registrant { get; set; }
}

/// <summary>
/// 创建收入请求
/// </summary>
public class CreateIncomeRequest
{
    public long? AttendeeId { get; set; }
    public string? Type { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PayerName { get; set; }
    public string? PayerOrganization { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新收入请求
/// </summary>
public class UpdateIncomeRequest
{
    public long? AttendeeId { get; set; }
    public string? Type { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PayerName { get; set; }
    public string? PayerOrganization { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 收入查询请求
/// </summary>
public class IncomeQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Type { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// 批量登记收入请求
/// </summary>
public class BatchRegisterIncomeRequest
{
    /// <summary>参会人ID列表</summary>
    public List<long> AttendeeIds { get; set; } = new();
    /// <summary>收入类型</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>统一金额</summary>
    public decimal Amount { get; set; }
    /// <summary>支付方式</summary>
    public string? PaymentMethod { get; set; }
    /// <summary>支付日期</summary>
    public DateTime PaymentDate { get; set; }
    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 收入汇总DTO
/// </summary>
public class IncomeSummaryDto
{
    /// <summary>总收入金额</summary>
    public decimal TotalAmount { get; set; }
    /// <summary>收入笔数</summary>
    public int TotalCount { get; set; }
    /// <summary>按类型统计</summary>
    public List<IncomeTypeSummary> TypeSummaries { get; set; } = new();
}

/// <summary>
/// 收入类型统计子项
/// </summary>
public class IncomeTypeSummary
{
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}
