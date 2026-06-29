using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Points.Events;

namespace STOTOP.Module.Points.EventHandlers;

/// <summary>
/// 积分申请提交事件处理器 - 通知申请人 WorkHub 待办。
/// </summary>
public class PointApplicationSubmittedEventHandler : IEventHandler<PointApplicationSubmittedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<PointApplicationSubmittedEventHandler> _logger;

    public PointApplicationSubmittedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<PointApplicationSubmittedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(PointApplicationSubmittedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workHubNotifier.AddWorkItemAsync(@event.ApplicantId, new WorkItemDto
            {
                Id = @event.ApplicationId,
                Source = "points",
                Category = "todo",
                Priority = 2,
                Title = $"积分申请待审批：{@event.RequestedPoints} 分",
                Summary = string.IsNullOrEmpty(@event.Reason) ? "积分申请" : @event.Reason,
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.ApplicantId);

            _logger.LogInformation("积分申请提交事件处理完成: ApplicationId={ApplicationId}, ApplicantId={ApplicantId}",
                @event.ApplicationId, @event.ApplicantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理积分申请提交事件失败: ApplicationId={ApplicationId}", @event.ApplicationId);
        }
    }
}

/// <summary>
/// 积分申请审批通过事件处理器 - 通知申请人审批结果。
/// </summary>
public class PointApplicationApprovedEventHandler : IEventHandler<PointApplicationApprovedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<PointApplicationApprovedEventHandler> _logger;

    public PointApplicationApprovedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<PointApplicationApprovedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(PointApplicationApprovedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workHubNotifier.RemoveWorkItemAsync(@event.ApplicantId, @event.ApplicationId, "points");
            await _workHubNotifier.AddWorkItemAsync(@event.ApplicantId, new WorkItemDto
            {
                Id = @event.ApplicationId,
                Source = "points",
                Category = "notification",
                Priority = 2,
                Title = $"积分申请已通过：+{@event.ApprovedPoints} 分",
                Summary = $"恭喜！您的积分申请已审批通过，获得 {@event.ApprovedPoints} 积分",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.ApplicantId);

            _logger.LogInformation("积分申请审批通过事件处理完成: ApplicationId={ApplicationId}, ApplicantId={ApplicantId}",
                @event.ApplicationId, @event.ApplicantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理积分申请审批通过事件失败: ApplicationId={ApplicationId}", @event.ApplicationId);
        }
    }
}

/// <summary>
/// 积分申请驳回事件处理器 - 通知申请人驳回结果。
/// </summary>
public class PointApplicationRejectedEventHandler : IEventHandler<PointApplicationRejectedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<PointApplicationRejectedEventHandler> _logger;

    public PointApplicationRejectedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<PointApplicationRejectedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(PointApplicationRejectedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workHubNotifier.RemoveWorkItemAsync(@event.ApplicantId, @event.ApplicationId, "points");
            await _workHubNotifier.AddWorkItemAsync(@event.ApplicantId, new WorkItemDto
            {
                Id = @event.ApplicationId,
                Source = "points",
                Category = "notification",
                Priority = 2,
                Title = "积分申请已驳回",
                Summary = $"您的积分申请未通过审批。原因：{(string.IsNullOrEmpty(@event.Reason) ? "无" : @event.Reason)}",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.ApplicantId);

            _logger.LogInformation("积分申请驳回事件处理完成: ApplicationId={ApplicationId}, ApplicantId={ApplicantId}",
                @event.ApplicationId, @event.ApplicantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理积分申请驳回事件失败: ApplicationId={ApplicationId}", @event.ApplicationId);
        }
    }
}
