using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 任务评论列表DTO（含表情统计+附件）
/// </summary>
public class TaskCommentListDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Type { get; set; }
    public long ParentCommentId { get; set; }
    public bool PushedToDingTalk { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<ReactionSummaryDto> Reactions { get; set; } = new();
    public List<AttachmentListDto> Attachments { get; set; } = new();
    public List<TaskCommentListDto> Replies { get; set; } = new();
}

/// <summary>
/// 表情统计DTO
/// </summary>
public class ReactionSummaryDto
{
    public string EmojiCode { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool HasReacted { get; set; }
    public List<long> UserIds { get; set; } = new();
}

/// <summary>
/// 创建评论请求（支持@提及）
/// </summary>
public class CreateTaskCommentRequest
{
    public string Content { get; set; } = string.Empty;
    public int Type { get; set; } = 0;
    public long ParentCommentId { get; set; } = 0;
    public List<long>? MentionUserIds { get; set; }
}

/// <summary>
/// 更新评论请求
/// </summary>
public class UpdateTaskCommentRequest
{
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 表情操作请求（添加/切换表情回应）
/// </summary>
public class ToggleReactionRequest
{
    public string EmojiCode { get; set; } = string.Empty;
}

/// <summary>
/// 评论分页查询请求
/// </summary>
public class CommentPagedRequest : PagedRequest
{
    public int? Type { get; set; }
}
