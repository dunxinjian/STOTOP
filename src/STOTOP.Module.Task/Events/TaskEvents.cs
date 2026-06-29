using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.Task.Events;

public class TaskAssignedEvent : BusinessEvent
{
    public long TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public long AssignerId { get; set; }
    public long AssigneeId { get; set; }
    public DateTime? Deadline { get; set; }
}

public class TaskStatusChangedEvent : BusinessEvent
{
    public long TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public long ChangedByUserId { get; set; }
}

public class TaskReminderDueEvent : BusinessEvent
{
    public long TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public long AssigneeId { get; set; }
    public DateTime Deadline { get; set; }
}
