namespace STOTOP.Core.Models;

/// <summary>
/// 工作项数据传输对象 - 用于 SignalR 推送
/// </summary>
public class WorkItemDto
{
    /// <summary>
    /// 工作项ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 来源模块: oa, quality, task, dataimport, contract, points
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// 分类: alert, reminder, todo, notification
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// 摘要
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// 优先级: 1=低, 2=中, 3=高, 4=紧急
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 关联URL
    /// </summary>
    public string? RelatedUrl { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 扩展元数据
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
