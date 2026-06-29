using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 触发事件队列：条件触发模式下，外部事件入队，等待批次合并/调度纳入
/// 状态机：0=待处理, 1=已纳入批次, 2=已过期
/// </summary>
public class CfTriggerEventQueue : BaseEntity
{
    public long FFlowDefinitionId { get; set; }
    /// <summary>事件类型（业务自定义，如 voucherPosted、taskFinished 等）</summary>
    public string FEventType { get; set; } = string.Empty;
    /// <summary>事件载荷 JSON（业务上下文）</summary>
    public string? FEventDataJson { get; set; }
    /// <summary>0=待处理, 1=已纳入批次, 2=已过期</summary>
    public int FStatus { get; set; }
    public long? FBatchId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FProcessedTime { get; set; }
}
