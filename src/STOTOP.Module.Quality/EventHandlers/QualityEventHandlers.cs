using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Quality.Events;

namespace STOTOP.Module.Quality.EventHandlers;

/// <summary>
/// 异常创建事件处理器 - 推送 WorkHub 告警。
/// </summary>
public class ExceptionCreatedEventHandler : IEventHandler<ExceptionCreatedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<ExceptionCreatedEventHandler> _logger;

    public ExceptionCreatedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<ExceptionCreatedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async global::System.Threading.Tasks.Task HandleAsync(ExceptionCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workHubNotifier.AddWorkItemAsync(@event.CreatorId, new WorkItemDto
            {
                Id = @event.ExceptionId,
                Source = "quality",
                Category = "alert",
                Priority = @event.Priority == "高" ? 3 : (@event.Priority == "紧急" ? 4 : 2),
                Title = $"质量异常：{@event.Title}",
                Summary = $"检测到质量异常，优先级：{@event.Priority}",
                CreatedAt = DateTime.Now,
            });

            await _workHubNotifier.RefreshStatsAsync(@event.CreatorId);

            _logger.LogInformation("处理异常创建事件成功: ExceptionId={ExceptionId}", @event.ExceptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理异常创建事件失败: ExceptionId={ExceptionId}", @event.ExceptionId);
        }
    }
}

/// <summary>
/// 异常派发事件处理器 - 为被指派人推送 WorkHub 告警。
/// </summary>
public class ExceptionDispatchedEventHandler : IEventHandler<ExceptionDispatchedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<ExceptionDispatchedEventHandler> _logger;

    public ExceptionDispatchedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<ExceptionDispatchedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async global::System.Threading.Tasks.Task HandleAsync(ExceptionDispatchedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workHubNotifier.AddWorkItemAsync(@event.AssigneeId, new WorkItemDto
            {
                Id = @event.ExceptionId,
                Source = "quality",
                Category = "alert",
                Priority = 3,
                Title = $"质量异常派发：{@event.Title}",
                Summary = $"您有一条质量异常需要处理，派发方式：{@event.DispatchMethod}",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.AssigneeId);

            _logger.LogInformation("处理异常派发事件成功: ExceptionId={ExceptionId}, AssigneeId={AssigneeId}",
                @event.ExceptionId, @event.AssigneeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理异常派发事件失败: ExceptionId={ExceptionId}", @event.ExceptionId);
        }
    }
}

/// <summary>
/// 异常关闭事件处理器 - 移除相关 WorkHub 工作项。
/// </summary>
public class ExceptionClosedEventHandler : IEventHandler<ExceptionClosedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<ExceptionClosedEventHandler> _logger;

    public ExceptionClosedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<ExceptionClosedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async global::System.Threading.Tasks.Task HandleAsync(ExceptionClosedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workHubNotifier.RemoveWorkItemAsync(@event.ClosedByUserId, @event.ExceptionId, "quality");
            await _workHubNotifier.RefreshStatsAsync(@event.ClosedByUserId);

            _logger.LogInformation("处理异常关闭事件成功: ExceptionId={ExceptionId}", @event.ExceptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理异常关闭事件失败: ExceptionId={ExceptionId}", @event.ExceptionId);
        }
    }
}
