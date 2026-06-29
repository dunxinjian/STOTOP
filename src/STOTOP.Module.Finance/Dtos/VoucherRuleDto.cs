namespace STOTOP.Module.Finance.Dtos;

/// <summary>
/// 凭证手动规则详情 DTO
/// </summary>
public class VoucherRuleDto
{
    public long Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public long? ChannelId { get; set; }
    public string? ChannelName { get; set; }
    public string? MatchCondition { get; set; }
    public string? DebitAccount { get; set; }
    public string? CreditAccount { get; set; }
    public string? SummaryTemplate { get; set; }
    public int Priority { get; set; }
    public int Status { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? UpdaterName { get; set; }
    public DateTime? UpdatedTime { get; set; }
}

/// <summary>
/// 创建凭证规则请求
/// </summary>
public class CreateVoucherRuleRequest
{
    public string RuleName { get; set; } = string.Empty;
    public long? ChannelId { get; set; }
    public string? MatchCondition { get; set; }
    public string? DebitAccount { get; set; }
    public string? CreditAccount { get; set; }
    public string? SummaryTemplate { get; set; }
    public int Priority { get; set; }
}

/// <summary>
/// 更新凭证规则请求
/// </summary>
public class UpdateVoucherRuleRequest
{
    public string RuleName { get; set; } = string.Empty;
    public long? ChannelId { get; set; }
    public string? MatchCondition { get; set; }
    public string? DebitAccount { get; set; }
    public string? CreditAccount { get; set; }
    public string? SummaryTemplate { get; set; }
    public int Priority { get; set; }
    public int Status { get; set; }
}

/// <summary>
/// 凭证规则查询请求
/// </summary>
public class VoucherRuleQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? ChannelId { get; set; }
    public int? Status { get; set; }
    public string? Keyword { get; set; }
}

/// <summary>
/// 凭证自动生成结果
/// </summary>
public class VoucherGenerateResult
{
    public int TotalProcessed { get; set; }
    public int GeneratedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 资金管理统计 DTO
/// </summary>
public class FundStatisticsDto
{
    public int TotalImported { get; set; }
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public int SkipMatchedCount { get; set; }
    public int VoucherGeneratedCount { get; set; }
    public decimal MatchRate { get; set; }
    public decimal VoucherRate { get; set; }
}
