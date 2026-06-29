using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.System.Events;

public class DingTalkSyncCompletedEvent : BusinessEvent
{
    public string SyncType { get; set; } = string.Empty;
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public long AdminUserId { get; set; }
}

public class SystemAlertEvent : BusinessEvent
{
    public string AlertType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<long> TargetUserIds { get; set; } = new();
}
