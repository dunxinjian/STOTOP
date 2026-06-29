namespace STOTOP.Module.Quality.Dtos;

/// <summary>
/// 看板统计DTO
/// </summary>
public class DashboardStatsDto
{
    /// <summary>异常总数</summary>
    public int TotalExceptions { get; set; }
    /// <summary>待处理数</summary>
    public int PendingCount { get; set; }
    /// <summary>处理中数</summary>
    public int ProcessingCount { get; set; }
    /// <summary>已超时数</summary>
    public int OverdueCount { get; set; }
    /// <summary>已关闭数</summary>
    public int ClosedCount { get; set; }
    /// <summary>今日新增</summary>
    public int TodayNewCount { get; set; }
    /// <summary>本周新增</summary>
    public int WeekNewCount { get; set; }
    /// <summary>平均处理时长(小时)</summary>
    public double AvgResolutionHours { get; set; }
    /// <summary>规则数量</summary>
    public int RuleCount { get; set; }
    /// <summary>知识库文章数</summary>
    public int KnowledgeCount { get; set; }
    /// <summary>超时率(%)</summary>
    public double OverdueRate { get; set; }
}

/// <summary>
/// 趋势数据点
/// </summary>
public class TrendDataPoint
{
    /// <summary>日期 yyyy-MM-dd</summary>
    public string Date { get; set; } = string.Empty;
    /// <summary>数量</summary>
    public int Count { get; set; }
}

/// <summary>
/// 分布项
/// </summary>
public class DistributionItem
{
    /// <summary>名称</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>值</summary>
    public int Value { get; set; }
}

/// <summary>
/// 趋势分析DTO
/// </summary>
public class TrendAnalysisDto
{
    /// <summary>日期/周/月标签</summary>
    public string Period { get; set; } = string.Empty;
    /// <summary>该周期总异常数</summary>
    public int Total { get; set; }
    /// <summary>数据异常(Type=0)</summary>
    public int DataException { get; set; }
    /// <summary>流程异常(Type=1)</summary>
    public int ProcessException { get; set; }
    /// <summary>业务异常(Type=2)</summary>
    public int BusinessException { get; set; }
}

/// <summary>
/// 处理效率分析DTO
/// </summary>
public class EfficiencyAnalysisDto
{
    /// <summary>平均处理时长(小时)</summary>
    public double AvgResolutionHours { get; set; }
    /// <summary>总关闭数</summary>
    public int TotalClosed { get; set; }
    /// <summary>总超时数</summary>
    public int TotalOverdue { get; set; }
    /// <summary>超时率(%)</summary>
    public double OverdueRate { get; set; }
    /// <summary>按类型分组</summary>
    public List<TypeEfficiency> ByType { get; set; } = new();
    /// <summary>按优先级分组</summary>
    public List<PriorityEfficiency> ByPriority { get; set; } = new();
}

public class TypeEfficiency
{
    public string TypeName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AvgHours { get; set; }
}

public class PriorityEfficiency
{
    public string PriorityName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AvgHours { get; set; }
}

/// <summary>
/// 来源分布DTO
/// </summary>
public class SourceDistributionDto
{
    public string Source { get; set; } = string.Empty;
    public int Count { get; set; }
    /// <summary>百分比</summary>
    public double Percentage { get; set; }
}

/// <summary>
/// 处理人统计DTO
/// </summary>
public class HandlerStatsDto
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    /// <summary>处理数量</summary>
    public int HandleCount { get; set; }
    /// <summary>已关闭数</summary>
    public int ClosedCount { get; set; }
    /// <summary>超时数</summary>
    public int OverdueCount { get; set; }
    /// <summary>平均处理时长</summary>
    public double AvgResolutionHours { get; set; }
    /// <summary>超时率(%)</summary>
    public double OverdueRate { get; set; }
}
