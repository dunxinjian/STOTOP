namespace STOTOP.Module.Points.Dtos;

/// <summary>
/// 积分触发事件 DTO（跨模块集成使用）
/// </summary>
public class PointEventDto
{
    /// <summary>事件类型（如 task.completed, task.overdue, manual.award）</summary>
    public string EventType { get; set; } = string.Empty;
    /// <summary>目标用户ID</summary>
    public long UserId { get; set; }
    /// <summary>组织ID</summary>
    public long OrgId { get; set; }
    /// <summary>来源模块编码（如 task, oa, quality）</summary>
    public string SourceModule { get; set; } = string.Empty;
    /// <summary>关联实体类型（如 task, goal, review, knowledge）</summary>
    public string? EntityType { get; set; }
    /// <summary>关联实体ID</summary>
    public long? EntityId { get; set; }
    /// <summary>上下文数据（JSON，如优先级、评分等附加信息）</summary>
    public object? Context { get; set; }
}
