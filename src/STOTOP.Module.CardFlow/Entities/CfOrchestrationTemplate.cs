using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 编排模板：定义跨流程编排的 DAG 模板（节点/边/数据协议）。
/// 状态机：draft / published / disabled
/// </summary>
public class CfOrchestrationTemplate : BaseEntity, IOrgScoped
{
    public string FCode { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public long FOrgId { get; set; }
    /// <summary>DAG 节点定义 JSON</summary>
    public string? FNodesJson { get; set; }
    /// <summary>DAG 边定义 JSON（含条件与数据协议）</summary>
    public string? FEdgesJson { get; set; }
    /// <summary>draft / published / disabled</summary>
    public string FStatus { get; set; } = "draft";
    /// <summary>防循环兜底：累计触发节点上限，默认 50</summary>
    public int FMaxTriggerCount { get; set; } = 50;
    public long FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FUpdatedTime { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
