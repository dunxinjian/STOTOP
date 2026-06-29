namespace STOTOP.Module.Points.Dtos;

/// <summary>
/// 积分账户 - 详情 DTO
/// </summary>
public class PointAccountDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>账户类型（1=A / 2=B）。默认返回 A+B 汇总时为 0。</summary>
    public int AccountType { get; set; }

    public int TotalPoints { get; set; }
    public int UsedPoints { get; set; }
    public int AvailablePoints { get; set; }
    public int MonthlyAward { get; set; }
    public int MonthlyDeduct { get; set; }
    public int YearlyPoints { get; set; }

    /// <summary>期初余额快照日期（B 分资本积累基准点）</summary>
    public DateTime? SnapshotDate { get; set; }
    /// <summary>期初余额快照值</summary>
    public int SnapshotValue { get; set; }

    /// <summary>A 分账户可用余额（GetAccountAsync 返回双账户结构时填充）</summary>
    public int FAPoints { get; set; }
    /// <summary>B 分账户可用余额（GetAccountAsync 返回双账户结构时填充）</summary>
    public int FBPoints { get; set; }

    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 积分统计看板 DTO
/// </summary>
public class PointStatisticsDto
{
    /// <summary>总积分</summary>
    public int TotalPoints { get; set; }
    /// <summary>可用积分</summary>
    public int AvailablePoints { get; set; }
    /// <summary>本月奖分</summary>
    public int MonthlyAward { get; set; }
    /// <summary>本月扣分</summary>
    public int MonthlyDeduct { get; set; }
    /// <summary>本年积分</summary>
    public int YearlyPoints { get; set; }
    /// <summary>当前排名</summary>
    public int? CurrentRank { get; set; }
    /// <summary>按来源统计</summary>
    public List<SourceStatItem> BySource { get; set; } = new();
    /// <summary>按月份趋势</summary>
    public List<TrendItem> MonthlyTrend { get; set; } = new();
}

/// <summary>
/// 来源统计项
/// </summary>
public class SourceStatItem
{
    public long SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int TotalPoints { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// 趋势数据项
/// </summary>
public class TrendItem
{
    public string Period { get; set; } = string.Empty;
    public int AwardPoints { get; set; }
    public int DeductPoints { get; set; }
    public int NetPoints { get; set; }
}
