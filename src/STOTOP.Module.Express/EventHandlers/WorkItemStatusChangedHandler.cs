using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.Express.EventHandlers;

/// <summary>
/// WorkItem 状态变更事件处理器（Express 模块扩展点）
/// 当前仅针对 QualityIssue 分类记录日志，后续可扩展其他业务逻辑
/// </summary>
public class WorkItemStatusChangedHandler : IEventHandler<WorkItemStatusChangedEvent>
{
    private readonly ILogger<WorkItemStatusChangedHandler> _logger;

    public WorkItemStatusChangedHandler(ILogger<WorkItemStatusChangedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(WorkItemStatusChangedEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.Category == "QualityIssue")
        {
            _logger.LogInformation(
                "质量问题 WorkItem {WorkItemId} 状态变更为 {NewStatus}，BizType={BizType}, BizId={BizId}",
                @event.WorkItemId, @event.NewStatus, @event.BizType, @event.BizId);

            // TODO: 后续可扩展：
            // - 状态为 Completed/Dismissed 时更新 DcImportError 的 FDispatchStatus
            // - 通知相关用户
            // - 触发后续流程
        }

        return Task.CompletedTask;
    }
}
