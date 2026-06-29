using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 编排实例：编排模板的一次运行实例。
/// 状态机：running / completed / terminated / failed / cancelled / paused
/// </summary>
public class CfOrchestrationInstance : BaseEntity, IOrgScoped
{
    public long FTemplateId { get; set; }
    public long FOrgId { get; set; }
    /// <summary>running / completed / terminated / failed / cancelled / paused</summary>
    public string FStatus { get; set; } = "running";
    /// <summary>reached_end / all_skipped / manual_cancel / node_failed</summary>
    public string? FCompletionReason { get; set; }
    /// <summary>创建时从模板快照的节点定义（运行时只读）</summary>
    public string? FSnapshotNodesJson { get; set; }
    /// <summary>创建时从模板快照的边定义（运行时只读）</summary>
    public string? FSnapshotEdgesJson { get; set; }
    /// <summary>编排级共享上下文</summary>
    public string? FContextJson { get; set; }
    /// <summary>累计触发节点次数（防循环兜底）</summary>
    public int FTriggerCount { get; set; }
    public long FInitiatorId { get; set; }
    public DateTime FInitiatedTime { get; set; }
    public DateTime? FCompletedTime { get; set; }
    public string? FFailureReason { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
