using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 知识库列表DTO（含浏览/点赞/收藏数）
/// </summary>
public class KnowledgeListDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Category { get; set; }
    public long OrgId { get; set; }
    public long AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public long? SourceReviewId { get; set; }
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public int CollectCount { get; set; }
    public int Status { get; set; }
    public bool IsPinned { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<TagSimpleDto> Tags { get; set; } = new();
}

/// <summary>
/// 知识库详情DTO（含评论列表）
/// </summary>
public class KnowledgeDetailDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public int Category { get; set; }
    public long OrgId { get; set; }
    public long AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public long? SourceReviewId { get; set; }
    public long? SourceTaskId { get; set; }
    public long? SourceProjectId { get; set; }
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public int CollectCount { get; set; }
    public int Status { get; set; }
    public bool IsPinned { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public bool HasLiked { get; set; }
    public bool HasCollected { get; set; }
    public List<TagSimpleDto> Tags { get; set; } = new();
    public List<AttachmentListDto> Attachments { get; set; } = new();
    public List<KnowledgeCommentDto> Comments { get; set; } = new();
}

/// <summary>
/// 知识评论DTO
/// </summary>
public class KnowledgeCommentDto
{
    public long Id { get; set; }
    public long KnowledgeId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public string Content { get; set; } = string.Empty;
    public long ParentCommentId { get; set; }
    public DateTime CreateTime { get; set; }
    public List<KnowledgeCommentDto> Replies { get; set; } = new();
}

/// <summary>
/// 创建知识文章请求
/// </summary>
public class CreateKnowledgeRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public int Category { get; set; }
    public long? SourceReviewId { get; set; }
    public long? SourceTaskId { get; set; }
    public long? SourceProjectId { get; set; }
    public List<long>? TagIds { get; set; }
}

/// <summary>
/// 更新知识文章请求
/// </summary>
public class UpdateKnowledgeRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public int Category { get; set; }
    public int Status { get; set; }
    public bool IsPinned { get; set; }
    public List<long>? TagIds { get; set; }
}

/// <summary>
/// 创建知识评论请求
/// </summary>
public class CreateKnowledgeCommentRequest
{
    public string Content { get; set; } = string.Empty;
    public long ParentCommentId { get; set; } = 0;
}

/// <summary>
/// 知识库查询请求（含分类+关键词+标签筛选）
/// </summary>
public class KnowledgePagedRequest : PagedRequest
{
    public int? Category { get; set; }
    public List<long>? TagIds { get; set; }
    public long? AuthorId { get; set; }
    public int? Status { get; set; }
    public bool? IsPinned { get; set; }
}
