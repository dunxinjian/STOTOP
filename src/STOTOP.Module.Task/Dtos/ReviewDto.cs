using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 复盘记录列表DTO
/// </summary>
public class ReviewListDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public int RelationType { get; set; }
    public long RelationId { get; set; }
    public string? RelationTitle { get; set; }
    public long OrgId { get; set; }
    public string Title { get; set; } = string.Empty;
    public long ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 复盘详情DTO（含四象限+附件）
/// </summary>
public class ReviewDetailDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public int RelationType { get; set; }
    public long RelationId { get; set; }
    public string? RelationTitle { get; set; }
    public long OrgId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? WentWell { get; set; }
    public string? ToImprove { get; set; }
    public string? LessonsLearned { get; set; }
    public string? ActionPlan { get; set; }
    public long ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public string? ParticipantIds { get; set; }
    public List<ParticipantDto>? Participants { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<AttachmentListDto> Attachments { get; set; } = new();
}

/// <summary>
/// 参与者简要DTO
/// </summary>
public class ParticipantDto
{
    public long UserId { get; set; }
    public string? UserName { get; set; }
}

/// <summary>
/// 创建复盘记录请求
/// </summary>
public class CreateReviewRequest
{
    public int RelationType { get; set; }
    public long RelationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? WentWell { get; set; }
    public string? ToImprove { get; set; }
    public string? LessonsLearned { get; set; }
    public string? ActionPlan { get; set; }
    public List<long>? ParticipantIds { get; set; }
}

/// <summary>
/// 更新复盘记录请求
/// </summary>
public class UpdateReviewRequest
{
    public string Title { get; set; } = string.Empty;
    public string? WentWell { get; set; }
    public string? ToImprove { get; set; }
    public string? LessonsLearned { get; set; }
    public string? ActionPlan { get; set; }
    public List<long>? ParticipantIds { get; set; }
}

/// <summary>
/// 提炼知识请求（从复盘提炼为知识库文章）
/// </summary>
public class ExtractKnowledgeRequest
{
    public string Title { get; set; } = string.Empty;
    public int Category { get; set; }
    public List<long>? TagIds { get; set; }
}

/// <summary>
/// 复盘查询请求（按关联类型查询）
/// </summary>
public class ReviewPagedRequest : PagedRequest
{
    public int? RelationType { get; set; }
    public long? RelationId { get; set; }
    public long? ReviewerId { get; set; }
    public int? Status { get; set; }
}
