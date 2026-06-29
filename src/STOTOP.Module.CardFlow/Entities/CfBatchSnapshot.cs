using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 批次快照：每个 AutoPlugin 执行前后的数据快照（迁移自 CfBatchSnapshot）
/// </summary>
public class CfBatchSnapshot : BaseEntity
{
    /// <summary>关联批次ID（CfBatch.FID）</summary>
    public long FBatchId { get; set; }
    /// <summary>AutoPlugin 序号</summary>
    public int FAutoPluginIndex { get; set; }
    /// <summary>AutoPlugin 名称</summary>
    public string FAutoPluginName { get; set; } = string.Empty;
    /// <summary>快照类型：Before/After 等</summary>
    public string FSnapshotType { get; set; } = string.Empty;
    /// <summary>暂存表名</summary>
    public string? FStagingTable { get; set; }
    /// <summary>快照数据 JSON</summary>
    public string? FSnapshotData { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
