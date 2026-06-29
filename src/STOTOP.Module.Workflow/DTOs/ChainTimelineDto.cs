namespace STOTOP.Module.Workflow.DTOs;

public class ChainTimelineDto
{
    public string ChainId { get; set; } = string.Empty;
    public List<WorkItemDto> WorkItems { get; set; } = new();
    public List<ChainEventDto> Events { get; set; } = new();
    public List<ChainParticipantDto> Participants { get; set; } = new();
}

public class ChainEventDto
{
    public int EventType { get; set; }
    public string Description { get; set; } = string.Empty;
    public long? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public DateTime Time { get; set; }
}

public class ChainParticipantDto
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;  // Creator/Assignee/Follower
}

public class ChainCommentDto
{
    public long Id { get; set; }
    public long AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string Content { get; set; } = string.Empty;
    public long? ReplyToId { get; set; }
    public DateTime CreateTime { get; set; }
}

public class ChainFollowerDto
{
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public bool IsMuted { get; set; }
}
