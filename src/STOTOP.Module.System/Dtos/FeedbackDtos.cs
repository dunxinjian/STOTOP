using STOTOP.Core.Models;

namespace STOTOP.Module.System.Dtos;

public class FeedbackCardDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public long OrgId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Type { get; set; }
    public string Module { get; set; } = string.Empty;
    public int Severity { get; set; }
    public int Status { get; set; }
    public long SubmitterId { get; set; }
    public string? SubmitterName { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string? Description { get; set; }
    public string? ReproduceSteps { get; set; }
    public string? ExpectedResult { get; set; }
    public string? ActualResult { get; set; }
    public string? AttachmentLinks { get; set; }
    public string? PageUrl { get; set; }
    public string? ClientInfo { get; set; }
    public string? Version { get; set; }
    public string? Conclusion { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public DateTime? ClosedTime { get; set; }
}

public class FeedbackActivityDto
{
    public long Id { get; set; }
    public long FeedbackId { get; set; }
    public long ActorId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Content { get; set; }
    public int? FromStatus { get; set; }
    public int? ToStatus { get; set; }
    public DateTime CreateTime { get; set; }
}

public class FeedbackDetailDto : FeedbackCardDto
{
    public List<FeedbackActivityDto> Activities { get; set; } = new();
}

public class FeedbackQueryRequest : PagedRequest
{
    public int? Type { get; set; }
    public string? Module { get; set; }
    public int? Severity { get; set; }
    public int? Status { get; set; }
    public long? SubmitterId { get; set; }
    public long? AssigneeId { get; set; }
    public bool Mine { get; set; }
}

public class CreateFeedbackRequest
{
    public string Title { get; set; } = string.Empty;
    public int Type { get; set; }
    public string Module { get; set; } = string.Empty;
    public int Severity { get; set; } = 2;
    public string? Description { get; set; }
    public string? ReproduceSteps { get; set; }
    public string? ExpectedResult { get; set; }
    public string? ActualResult { get; set; }
    public string? AttachmentLinks { get; set; }
    public string? PageUrl { get; set; }
    public string? ClientInfo { get; set; }
    public string? Version { get; set; }
}

public class UpdateFeedbackRequest : CreateFeedbackRequest
{
    public long? AssigneeId { get; set; }
    public string? Conclusion { get; set; }
}

public class TransitionFeedbackRequest
{
    public int Status { get; set; }
    public string? Comment { get; set; }
    public string? Conclusion { get; set; }
}

public class AssignFeedbackRequest
{
    public long? AssigneeId { get; set; }
    public string? Comment { get; set; }
}

public class AddFeedbackCommentRequest
{
    public string Content { get; set; } = string.Empty;
}

public class FeedbackStatusCountDto
{
    public int Status { get; set; }
    public int Count { get; set; }
}
