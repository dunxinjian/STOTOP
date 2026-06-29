namespace STOTOP.Module.CardFlow.Models;

/// <summary>
/// DryRun 预演结果
/// </summary>
public class DryRunResult
{
    public int TotalRows { get; set; }
    public int MatchedRows { get; set; }
    public int UnmatchedRows { get; set; }
    public int EstimatedVouchers { get; set; }
    public List<DryRunGroupDetail> GroupDetails { get; set; } = new();

    /// <summary>全量未匹配行（上限 500 行）</summary>
    public List<Dictionary<string, object>> UnmatchedDetails { get; set; } = new();

    /// <summary>是否还有更多未匹配行未返回（超出 500 行上限时为 true）</summary>
    public bool HasMoreUnmatched { get; set; }

    /// <summary>按指定字段对未匹配行的分组汇总</summary>
    public List<DryRunGroupedSummary> GroupedSummary { get; set; } = new();
}

public class DryRunGroupDetail
{
    public int GroupIndex { get; set; }
    public int LineNo { get; set; }
    public int Matched { get; set; }
}

/// <summary>
/// 未匹配行按字段汇总
/// </summary>
public class DryRunGroupedSummary
{
    /// <summary>字段值</summary>
    public string FieldValue { get; set; } = string.Empty;

    /// <summary>未匹配行数</summary>
    public int Count { get; set; }

    /// <summary>汇总金额（有金额相关字段时才计算）</summary>
    public decimal? TotalAmount { get; set; }
}
