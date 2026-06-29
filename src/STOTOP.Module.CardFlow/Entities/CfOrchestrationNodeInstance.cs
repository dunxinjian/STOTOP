using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 编排节点实例：编排实例中每个 DAG 节点的运行态记录。
/// 通过 (F编排实例ID, F节点ID) 唯一约束实现幂等性。
/// 状态机：pending / running / completed / skipped / failed
/// </summary>
public class CfOrchestrationNodeInstance : BaseEntity
{
    public long FOrchestrationInstanceId { get; set; }
    /// <summary>DAG 中节点的字符串 ID</summary>
    public string FNodeId { get; set; } = string.Empty;
    /// <summary>pending / running / completed / skipped / failed</summary>
    public string FStatus { get; set; } = "pending";
    /// <summary>仅 cardflow 节点：completed / voided / cancelled</summary>
    public string? FEndStatusType { get; set; }
    /// <summary>关联卡片 ID（single 模式）</summary>
    public long? FRelatedCardId { get; set; }
    /// <summary>关联批次 ID（batch 模式）</summary>
    public long? FRelatedBatchId { get; set; }
    /// <summary>节点执行结果摘要 JSON</summary>
    public string? FResultJson { get; set; }
    public DateTime? FStartTime { get; set; }
    public DateTime? FCompletedTime { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
