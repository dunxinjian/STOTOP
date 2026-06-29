namespace STOTOP.Module.Finance.Dtos;

/// <summary>
/// 银行流水详情 DTO
/// </summary>
public class BankTransactionDto
{
    public long Id { get; set; }
    public long ChannelId { get; set; }
    public string? ChannelName { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionNo { get; set; } = string.Empty;
    public string? CounterpartAccount { get; set; }
    public string? CounterpartName { get; set; }
    public int Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public string? Summary { get; set; }
    public string? Remark { get; set; }
    public long? ImportBatchId { get; set; }
    public int MatchStatus { get; set; }
    public string? RelatedBusinessType { get; set; }
    public long? RelatedBusinessId { get; set; }
    public long? VoucherId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 银行流水查询请求
/// </summary>
public class BankTransactionQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? ChannelId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Direction { get; set; }
    public int? MatchStatus { get; set; }
    public string? CounterpartName { get; set; }
    public string? Keyword { get; set; }
}

/// <summary>
/// 银行流水导入项
/// </summary>
public class BankTransactionImportItem
{
    public long ChannelId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionNo { get; set; } = string.Empty;
    public string? CounterpartAccount { get; set; }
    public string? CounterpartName { get; set; }
    public int Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public string? Summary { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 流水导入请求
/// </summary>
public class BankTransactionImportRequest
{
    public long ChannelId { get; set; }
    public List<BankTransactionImportItem> Items { get; set; } = new();
}

/// <summary>
/// 人工匹配请求
/// </summary>
public class BankTransactionManualMatchRequest
{
    public long TransactionId { get; set; }
    public string BusinessType { get; set; } = string.Empty;
    public long BusinessId { get; set; }
}

/// <summary>
/// 无需匹配标记请求
/// </summary>
public class BankTransactionSkipMatchRequest
{
    public List<long> TransactionIds { get; set; } = new();
}

/// <summary>
/// 自动匹配结果
/// </summary>
public class AutoMatchResult
{
    public int TotalProcessed { get; set; }
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
}

/// <summary>
/// 流水导入结果
/// </summary>
public class BankTransactionImportResult
{
    public int TotalReceived { get; set; }
    public int ImportedCount { get; set; }
    public int DuplicateCount { get; set; }
}
