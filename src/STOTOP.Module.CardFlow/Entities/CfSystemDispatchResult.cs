using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 系统派发结果（迁移自 CfSystemDispatchResult）
/// </summary>
public class CfSystemDispatchResult : BaseEntity
{
    /// <summary>关联批次ID（CfBatch.FID）</summary>
    public long FBatchId { get; set; }
    /// <summary>暂存表</summary>
    public string FStagingTable { get; set; } = string.Empty;
    /// <summary>派发规则ID（CfDispatchRule.FID）</summary>
    public long FDispatchRuleId { get; set; }
    /// <summary>规则名称</summary>
    public string? FRuleName { get; set; }
    /// <summary>严重级别</summary>
    public string? FSeverity { get; set; }
    /// <summary>命中行数</summary>
    public int FAffectedRowCount { get; set; }
    /// <summary>命中行ID JSON 数组</summary>
    public string? FAffectedRowIds { get; set; }
    /// <summary>处理状态：0=待处理 1=处理中 2=已处理 3=处理失败</summary>
    public int FProcessingStatus { get; set; }
    /// <summary>处理结果 JSON</summary>
    public string? FProcessingResult { get; set; }
    /// <summary>上下文 JSON</summary>
    public string? FContext { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
