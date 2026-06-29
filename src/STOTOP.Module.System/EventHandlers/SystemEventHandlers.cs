using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.System.Events;

namespace STOTOP.Module.System.EventHandlers;

/// <summary>
/// 钉钉同步完成事件处理器 - 推送同步结果到 WorkHub。
/// </summary>
public class DingTalkSyncCompletedEventHandler : IEventHandler<DingTalkSyncCompletedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<DingTalkSyncCompletedEventHandler> _logger;

    public DingTalkSyncCompletedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<DingTalkSyncCompletedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(DingTalkSyncCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetUserId = @event.AdminUserId > 0 ? @event.AdminUserId : 1L;

            await _workHubNotifier.AddWorkItemAsync(targetUserId, new WorkItemDto
            {
                Source = "system",
                Category = "notification",
                Priority = @event.FailCount > 0 ? 3 : 1,
                Title = $"钉钉同步完成：{@event.SyncType}",
                Summary = $"成功 {@event.SuccessCount} 条，失败 {@event.FailCount} 条",
                CreatedAt = @event.OccurredAt,
            });

            await _workHubNotifier.RefreshStatsAsync(targetUserId);

            _logger.LogInformation(
                "处理钉钉同步完成事件成功: SyncType={SyncType}, Success={Success}, Fail={Fail}, UserId={UserId}",
                @event.SyncType, @event.SuccessCount, @event.FailCount, targetUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理钉钉同步完成事件失败: SyncType={SyncType}", @event.SyncType);
        }
    }
}

/// <summary>
/// 系统告警事件处理器 - 推送告警通知到 WorkHub。
/// </summary>
public class SystemAlertEventHandler : IEventHandler<SystemAlertEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<SystemAlertEventHandler> _logger;

    public SystemAlertEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<SystemAlertEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(SystemAlertEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetUserIds = @event.TargetUserIds?.Count > 0
                ? @event.TargetUserIds
                : new List<long> { @event.TriggeredByUserId > 0 ? @event.TriggeredByUserId : 1L };

            foreach (var userId in targetUserIds)
            {
                var targetUserId = userId > 0 ? userId : 1L;

                await _workHubNotifier.AddWorkItemAsync(targetUserId, new WorkItemDto
                {
                    Source = "system",
                    Category = "alert",
                    Priority = 3,
                    Title = $"系统告警：{@event.Title}",
                    Summary = @event.Message,
                    CreatedAt = @event.OccurredAt,
                });

                await _workHubNotifier.RefreshStatsAsync(targetUserId);
            }

            _logger.LogInformation(
                "处理系统告警事件成功: AlertType={AlertType}, Title={Title}, TargetUsers={UserCount}",
                @event.AlertType, @event.Title, targetUserIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理系统告警事件失败: AlertType={AlertType}, Title={Title}",
                @event.AlertType, @event.Title);
        }
    }
}
