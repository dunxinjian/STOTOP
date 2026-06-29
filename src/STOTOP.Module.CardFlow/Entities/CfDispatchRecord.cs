using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 派发记录：编排自动派发与自由派发的统一历史记录。
/// 状态机：pending / triggered / skipped / failed
/// </summary>
public class CfDispatchRecord : BaseEntity
{
    /// <summary>关联编排实例（自由派发时为 null）</summary>
    public long? FOrchestrationInstanceId { get; set; }
    /// <summary>auto（自动编排）/ manual（自由派发）</summary>
    public string FDispatchType { get; set; } = "auto";
    public string? FSourceNodeId { get; set; }
    public long? FSourceCardId { get; set; }
    public string? FSourceFlowCode { get; set; }
    public string? FTargetNodeId { get; set; }
    public long? FTargetCardId { get; set; }
    public string? FTargetFlowCode { get; set; }
    /// <summary>三级数据协议内容 JSON</summary>
    public string? FDataPayloadJson { get; set; }
    /// <summary>pending / triggered / skipped / failed</summary>
    public string FStatus { get; set; } = "pending";
    /// <summary>自由派发时记录操作人</summary>
    public long? FOperatorId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FTriggeredTime { get; set; }
    public string? FFailureReason { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
