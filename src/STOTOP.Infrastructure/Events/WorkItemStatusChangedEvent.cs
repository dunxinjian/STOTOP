namespace STOTOP.Infrastructure.Events;

/// <summary>
/// 工作项状态变更事件
/// </summary>
public class WorkItemStatusChangedEvent : BusinessEvent
{
    /// <summary>工作项ID</summary>
    public long WorkItemId { get; set; }

    /// <summary>新状态</summary>
    public int NewStatus { get; set; }

    /// <summary>业务类型</summary>
    public string BizType { get; set; } = string.Empty;

    /// <summary>业务ID</summary>
    public long BizId { get; set; }

    /// <summary>分类</summary>
    public string? Category { get; set; }
}
