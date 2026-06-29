using STOTOP.Infrastructure.Events;
using STOTOP.Module.System.Events;

namespace STOTOP.Module.System.Services;

public interface ISystemAlertService
{
    Task PublishAlertAsync(string alertType, string title, string message, IEnumerable<long> targetUserIds);
}

public class SystemAlertService : ISystemAlertService
{
    private readonly IEventDispatcher _eventDispatcher;

    public SystemAlertService(IEventDispatcher eventDispatcher)
    {
        _eventDispatcher = eventDispatcher;
    }

    public async Task PublishAlertAsync(string alertType, string title, string message, IEnumerable<long> targetUserIds)
    {
        await _eventDispatcher.PublishAsync(new SystemAlertEvent
        {
            AlertType = alertType,
            Title = title,
            Message = message,
            TargetUserIds = targetUserIds.ToList(),
            TriggeredByUserId = 0, // 系统触发
            ModuleCode = "system"
        });
    }
}
