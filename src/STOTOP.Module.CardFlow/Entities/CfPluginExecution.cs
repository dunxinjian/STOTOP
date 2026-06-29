using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>插件单次执行记录（从 CfAgentExecution 迁移而来）</summary>
public class CfPluginExecution : BaseEntity
{
    /// <summary>所属批次ID（关联 CfBatch.FID）</summary>
    public long FBatchId { get; set; }
    /// <summary>组织ID（多租户隔离）</summary>
    public long FOrgId { get; set; }
    /// <summary>AutoPlugin 名称</summary>
    public string FAutoPluginName { get; set; } = string.Empty;
    /// <summary>在管道中的排序索引（0-based）</summary>
    public int FAutoPluginIndex { get; set; }
    /// <summary>10=待处理,11=进行中,12=已完成,13=失败,14=已跳过</summary>
    public int FStatus { get; set; }
    public DateTime FStartTime { get; set; }
    public DateTime? FEndTime { get; set; }
    public string? FErrorMessage { get; set; }
    /// <summary>插件执行摘要（JSON），记录处理行数、命中规则等</summary>
    public string? FSummaryJson { get; set; }
    public DateTime FCreatedTime { get; set; }
}
