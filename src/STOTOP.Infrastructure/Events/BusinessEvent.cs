namespace STOTOP.Infrastructure.Events;

/// <summary>
/// 业务事件基类 - 所有业务事件的抽象基类
/// </summary>
public abstract class BusinessEvent
{
    /// <summary>
    /// 触发事件的用户ID
    /// </summary>
    public long TriggeredByUserId { get; set; }

    /// <summary>
    /// 事件发生时间
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 模块代码 - 标识事件来源模块
    /// </summary>
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>
    /// 事件ID - 唯一标识
    /// </summary>
    public Guid EventId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 组织ID - 多租户隔离上下文（跨组织事件可留 0）
    /// </summary>
    public long OrgId { get; set; }
}
