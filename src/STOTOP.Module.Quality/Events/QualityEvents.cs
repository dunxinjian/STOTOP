using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.Quality.Events;

public class ExceptionCreatedEvent : BusinessEvent
{
    public long ExceptionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public long CreatorId { get; set; }
}

public class ExceptionDispatchedEvent : BusinessEvent
{
    public long ExceptionId { get; set; }
    public long AssigneeId { get; set; }
    public string DispatchMethod { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class ExceptionClosedEvent : BusinessEvent
{
    public long ExceptionId { get; set; }
    public long ClosedByUserId { get; set; }
    public string Resolution { get; set; } = string.Empty;
}
