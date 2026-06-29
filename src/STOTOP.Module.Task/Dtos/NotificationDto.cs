using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 通知列表DTO
/// </summary>
public class NotificationListDto
{
    public long Id { get; set; }
    public long ReceiverId { get; set; }
    public int EventType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int RelationType { get; set; }
    public long RelationId { get; set; }
    public bool IsRead { get; set; }
    public bool PushedToDingTalk { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 未读数DTO
/// </summary>
public class UnreadCountDto
{
    public int Total { get; set; }
    public Dictionary<int, int> ByEventType { get; set; } = new();
}

/// <summary>
/// 通知查询请求（含已读/未读筛选）
/// </summary>
public class NotificationPagedRequest : PagedRequest
{
    public bool? IsRead { get; set; }
    public int? EventType { get; set; }
    public int? RelationType { get; set; }
}
