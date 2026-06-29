namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 归档统计
/// </summary>
public class ArchiveStatsDto
{
    /// <summary>待归档运单数</summary>
    public long PendingCount { get; set; }
    /// <summary>已归档运单数</summary>
    public long ArchivedCount { get; set; }
}

/// <summary>
/// 归档执行结果
/// </summary>
public class ArchiveResultDto
{
    /// <summary>归档运单条数</summary>
    public long WaybillCount { get; set; }
    /// <summary>归档计费结果条数</summary>
    public long BillingResultCount { get; set; }
    /// <summary>归档成本明细条数</summary>
    public long CostBreakdownCount { get; set; }
    /// <summary>执行耗时(毫秒)</summary>
    public long ElapsedMs { get; set; }
}
