using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.CardFlow.Events;

namespace STOTOP.Module.CardFlow.EventHandlers;

/// <summary>
/// 批次生命周期审计处理器 - 记录批次撤销和物理删除的审计日志
/// </summary>
public class BatchLifecycleAuditHandler :
    IEventHandler<ImportBatchPurgedEvent>,
    IEventHandler<ImportBatchRevokedEvent>
{
    private readonly ILogger<BatchLifecycleAuditHandler> _logger;

    public BatchLifecycleAuditHandler(ILogger<BatchLifecycleAuditHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(ImportBatchPurgedEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("审计: 批次 {BatchId} 已被用户 {OperatorId} 物理删除，目标表={TargetTable}",
            evt.BatchId, evt.OperatorId, evt.TargetTable);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ImportBatchRevokedEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("审计: 批次 {BatchId} 已被用户 {OperatorId} 撤销",
            evt.BatchId, evt.OperatorId);
        return Task.CompletedTask;
    }
}
