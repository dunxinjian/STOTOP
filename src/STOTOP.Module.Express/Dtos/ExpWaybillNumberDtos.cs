namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 运单号段DTO
/// </summary>
public class WaybillNumberPoolDto
{
    public long Id { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string? Prefix { get; set; }
    public string? StartNo { get; set; }
    public string? EndNo { get; set; }
    public int? TotalCount { get; set; }
    public int Allocated { get; set; }
    public int? Remaining { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 运单号交易DTO
/// </summary>
public class WaybillNumberTransactionDto
{
    public long Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public long? PoolId { get; set; }
    public int? TransactionType { get; set; }
    public int Quantity { get; set; }
    public string? StartNo { get; set; }
    public string? EndNo { get; set; }
    public DateTime? TransactionDate { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 客户运单号余额DTO
/// </summary>
public class ClientWaybillBalanceDto
{
    public long Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public int Available { get; set; }
    public int Used { get; set; }
    public int TotalAllocated { get; set; }
    public int TotalReturned { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 分配请求
/// </summary>
public class AllocateRequest
{
    public long PoolId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>
/// 回收请求
/// </summary>
public class ReturnRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>
/// 创建号段请求
/// </summary>
public class CreatePoolRequest
{
    public string BrandCode { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string? StartNo { get; set; }
    public string? EndNo { get; set; }
    public int? TotalCount { get; set; }
}
