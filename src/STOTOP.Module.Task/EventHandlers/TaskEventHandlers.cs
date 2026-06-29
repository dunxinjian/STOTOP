using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Task.Events;

namespace STOTOP.Module.Task.EventHandlers;

/// <summary>
/// 任务分配事件处理器 - 为被分配人推送 WorkHub 待办。
/// </summary>
public class TaskAssignedEventHandler : IEventHandler<TaskAssignedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<TaskAssignedEventHandler> _logger;

    public TaskAssignedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<TaskAssignedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async global::System.Threading.Tasks.Task HandleAsync(TaskAssignedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workHubNotifier.AddWorkItemAsync(@event.AssigneeId, new WorkItemDto
            {
                Id = @event.TaskId,
                Source = "task",
                Category = "todo",
                Priority = 2,
                Title = $"新任务分配：{@event.TaskTitle}",
                Summary = "您被分配了一个新任务，请及时处理。",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.AssigneeId);

            _logger.LogInformation("任务分配事件处理完成: TaskId={TaskId}, AssigneeId={AssigneeId}",
                @event.TaskId, @event.AssigneeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理任务分配事件失败: TaskId={TaskId}", @event.TaskId);
        }
    }
}

/// <summary>
/// 任务状态变更事件处理器 - 更新相关 WorkHub 工作项。
/// </summary>
public class TaskStatusChangedEventHandler : IEventHandler<TaskStatusChangedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<TaskStatusChangedEventHandler> _logger;

    private static readonly Dictionary<string, string> StatusNames = new()
    {
        { "0", "待开始" },
        { "1", "进行中" },
        { "2", "已暂停" },
        { "3", "已完成" },
        { "4", "已取消" }
    };

    public TaskStatusChangedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<TaskStatusChangedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async global::System.Threading.Tasks.Task HandleAsync(TaskStatusChangedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var oldName = StatusNames.GetValueOrDefault(@event.OldStatus, @event.OldStatus);
            var newName = StatusNames.GetValueOrDefault(@event.NewStatus, @event.NewStatus);

            if (@event.NewStatus == "3" || @event.NewStatus == "4")
            {
                await _workHubNotifier.RemoveWorkItemAsync(@event.ChangedByUserId, @event.TaskId, "task");
            }
            else
            {
                await _workHubNotifier.UpdateWorkItemAsync(@event.ChangedByUserId, new WorkItemDto
                {
                    Id = @event.TaskId,
                    Source = "task",
                    Category = "todo",
                    Priority = 2,
                    Title = $"任务状态变更：{@event.TaskTitle}",
                    Summary = $"任务状态已从「{oldName}」变更为「{newName}」",
                    CreatedAt = DateTime.Now,
                });
            }
            await _workHubNotifier.RefreshStatsAsync(@event.ChangedByUserId);

            _logger.LogInformation("任务状态变更事件处理完成: TaskId={TaskId}, {OldStatus}->{NewStatus}",
                @event.TaskId, @event.OldStatus, @event.NewStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理任务状态变更事件失败: TaskId={TaskId}", @event.TaskId);
        }
    }
}

/// <summary>
/// 任务到期提醒事件处理器 - 为负责人推送 WorkHub 提醒。
/// </summary>
public class TaskReminderDueEventHandler : IEventHandler<TaskReminderDueEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<TaskReminderDueEventHandler> _logger;

    public TaskReminderDueEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<TaskReminderDueEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async global::System.Threading.Tasks.Task HandleAsync(TaskReminderDueEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workHubNotifier.AddWorkItemAsync(@event.AssigneeId, new WorkItemDto
            {
                Id = @event.TaskId,
                Source = "task",
                Category = "reminder",
                Priority = 3,
                Title = $"任务到期提醒：{@event.TaskTitle}",
                Summary = $"任务将于 {@event.Deadline:yyyy-MM-dd HH:mm} 到期",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.AssigneeId);

            _logger.LogInformation("任务到期提醒事件处理完成: TaskId={TaskId}, AssigneeId={AssigneeId}",
                @event.TaskId, @event.AssigneeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理任务到期提醒事件失败: TaskId={TaskId}", @event.TaskId);
        }
    }
}
