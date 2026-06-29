using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.CardFlow.Events;

namespace STOTOP.Module.CardFlow.EventHandlers;

/// <summary>
/// 导入批次完成事件处理器 - 推送导入结果到 WorkHub。
/// </summary>
public class ImportBatchCompletedEventHandler : IEventHandler<ImportBatchCompletedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<ImportBatchCompletedEventHandler> _logger;

    public ImportBatchCompletedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<ImportBatchCompletedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(ImportBatchCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            if (@event.OperatorId <= 0)
            {
                _logger.LogInformation("导入批次完成（无明确操作者）: BatchId={BatchId}, FileName={FileName}, 成功={Success}, 失败={Fail}",
                    @event.BatchId, @event.FileName, @event.SuccessRows, @event.FailRows);
                return;
            }

            await _workHubNotifier.AddWorkItemAsync(@event.OperatorId, new WorkItemDto
            {
                Id = @event.BatchId,
                Source = "datacenter",
                Category = @event.FailRows > 0 ? "alert" : "notification",
                Priority = @event.FailRows > 0 ? 3 : 2,
                Title = $"导入完成：{@event.FileName}",
                Summary = $"成功 {@event.SuccessRows} 行，失败 {@event.FailRows} 行",
                CreatedAt = DateTime.Now,
            });

            await _workHubNotifier.RefreshStatsAsync(@event.OperatorId);

            _logger.LogInformation("处理导入完成事件成功: BatchId={BatchId}, FileName={FileName}",
                @event.BatchId, @event.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理导入完成事件失败: BatchId={BatchId}", @event.BatchId);
        }
    }
}

/// <summary>
/// 导入批次失败事件处理器 - 推送失败告警到 WorkHub。
/// </summary>
public class ImportBatchFailedEventHandler : IEventHandler<ImportBatchFailedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<ImportBatchFailedEventHandler> _logger;

    public ImportBatchFailedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<ImportBatchFailedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(ImportBatchFailedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            if (@event.OperatorId <= 0)
            {
                _logger.LogInformation("导入批次失败（无明确操作者）: BatchId={BatchId}, FileName={FileName}, Error={Error}",
                    @event.BatchId, @event.FileName, @event.ErrorMessage);
                return;
            }

            await _workHubNotifier.AddWorkItemAsync(@event.OperatorId, new WorkItemDto
            {
                Id = @event.BatchId,
                Source = "datacenter",
                Category = "alert",
                Priority = 3,
                Title = $"导入失败：{@event.FileName}",
                Summary = @event.ErrorMessage.Length > 100 ? @event.ErrorMessage[..100] + "..." : @event.ErrorMessage,
                CreatedAt = DateTime.Now,
            });

            await _workHubNotifier.RefreshStatsAsync(@event.OperatorId);

            _logger.LogInformation("处理导入失败事件成功: BatchId={BatchId}, FileName={FileName}",
                @event.BatchId, @event.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理导入失败事件失败: BatchId={BatchId}", @event.BatchId);
        }
    }
}

/// <summary>
/// 导入错误派发事件处理器 - 通知被指派人。
/// </summary>
public class ImportErrorDispatchedEventHandler : IEventHandler<ImportErrorDispatchedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<ImportErrorDispatchedEventHandler> _logger;

    public ImportErrorDispatchedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<ImportErrorDispatchedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(ImportErrorDispatchedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            if (@event.AssigneeId <= 0) return;

            await _workHubNotifier.AddWorkItemAsync(@event.AssigneeId, new WorkItemDto
            {
                Id = @event.BatchId,
                Source = "datacenter",
                Category = "notification",
                Priority = 2,
                Title = $"导入异常派发：{@event.ErrorCount} 条",
                Summary = $"有 {@event.ErrorCount} 条导入异常需要您处理",
                CreatedAt = DateTime.Now,
            });
            await _workHubNotifier.RefreshStatsAsync(@event.AssigneeId);

            _logger.LogInformation("处理导入错误派发事件成功: BatchId={BatchId}, AssigneeId={AssigneeId}",
                @event.BatchId, @event.AssigneeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理导入错误派发事件失败: BatchId={BatchId}", @event.BatchId);
        }
    }
}

/// <summary>
/// 分类完成事件处理器 - 仅当有 Warning/Error/Critical 级别分类时推送 WorkHub 告警。
/// </summary>
public class ClassificationCompletedEventHandler : IEventHandler<ClassificationCompletedEvent>
{
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<ClassificationCompletedEventHandler> _logger;

    public ClassificationCompletedEventHandler(
        IWorkHubNotifier workHubNotifier,
        ILogger<ClassificationCompletedEventHandler> logger)
    {
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(ClassificationCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            if (@event.WarningCount <= 0 && @event.ErrorCount <= 0)
            {
                _logger.LogInformation("分类完成（无告警/异常）: BatchId={BatchId}, FileName={FileName}, Total={Total}",
                    @event.BatchId, @event.FileName, @event.TotalClassifications);
                return;
            }

            if (@event.OperatorId <= 0)
            {
                _logger.LogInformation("分类完成（无明确操作者）: BatchId={BatchId}, FileName={FileName}",
                    @event.BatchId, @event.FileName);
                return;
            }

            var summary = $"检测到 {@event.ErrorCount} 个异常，{@event.WarningCount} 个警告";
            if (@event.ClassificationTypes.Count > 0)
            {
                summary += $"；分类类型：{string.Join("、", @event.ClassificationTypes)}";
            }

            await _workHubNotifier.AddWorkItemAsync(@event.OperatorId, new WorkItemDto
            {
                Id = @event.BatchId,
                Source = "datacenter",
                Category = "alert",
                Priority = @event.ErrorCount > 0 ? 3 : 2,
                Title = $"数据异常：{@event.FileName}",
                Summary = summary,
                CreatedAt = DateTime.Now,
            });

            await _workHubNotifier.RefreshStatsAsync(@event.OperatorId);

            _logger.LogInformation("处理分类完成事件成功: BatchId={BatchId}, Errors={Errors}, Warnings={Warnings}",
                @event.BatchId, @event.ErrorCount, @event.WarningCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理分类完成事件失败: BatchId={BatchId}", @event.BatchId);
        }
    }
}
