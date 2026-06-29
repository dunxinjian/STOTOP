using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.WebAPI.Hubs;

namespace STOTOP.WebAPI.Services;

public class SignalRProgressNotifier : IProgressNotifier, IDingTalkSyncProgressNotifier
{
    private readonly IHubContext<ProgressHub> _hubContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SignalRProgressNotifier> _logger;

    /// <summary>节流记录：每个 key 上次推送的百分比</summary>
    private static readonly ConcurrentDictionary<string, int> _lastReportedPercent = new();

    public SignalRProgressNotifier(IHubContext<ProgressHub> hubContext, IServiceScopeFactory serviceScopeFactory, ILogger<SignalRProgressNotifier> logger)
    {
        _hubContext = hubContext;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task NotifyImportProgressAsync(long batchId, int processedRows, int totalRows, string stage)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var progressPercent = totalRows > 0 ? (int)((double)processedRows / totalRows * 100) : 0;
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("BatchProgress", new { batchId, progressPercent, processedRows, totalRows, stageLabel = stage }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyDownloadProgressAsync(long taskId, int currentStep, int totalSteps, string stepName)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group($"download-{taskId}")
                .SendAsync("OnDownloadProgress", new { taskId, currentStep, totalSteps, stepName }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyProcessingProgressAsync(long ruleId, int processed, int total, string status)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group("processing")
                .SendAsync("OnProcessingProgress", new { ruleId, processed, total, status }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyBatchStatusChangedAsync(long batchId, int newStatus, string statusText)
    {
        await NotifyBatchStatusChangedAsync(batchId, newStatus, statusText, null);
    }

    public async Task NotifyBatchStatusChangedAsync(long batchId, int newStatus, string statusText, BatchSummaryDto? summary)
    {
        await NotifyBatchStatusChangedAsync(batchId, newStatus, statusText, summary, 0);
    }

    public async Task NotifyBatchStatusChangedAsync(long batchId, int newStatus, string statusText, BatchSummaryDto? summary, long version)
    {
        // 将 int 状态码映射为前端期望的字符串: 1=Processing, 2=Completed, 3=Failed, 4=PartialCompleted, 5=PendingPipeline
        var status = newStatus switch
        {
            1 => "Processing",
            2 => "Completed",
            3 => "Failed",
            4 => "PartialCompleted",
            5 => "PendingPipeline",
            _ => "Processing"
        };
        var payload = new { batchId, status, statusText, summary = (object?)summary, version };

        // 关键状态变更（完成/失败/部分完成）推送失败时重试最多2次
        var isCritical = newStatus is 2 or 3 or 4;
        var maxAttempts = isCritical ? 3 : 1;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await _hubContext.Clients.Group($"import-{batchId}")
                    .SendAsync("BatchStatusChanged", payload, cts.Token)
                    .ConfigureAwait(false);
                return; // 推送成功，直接返回
            }
            catch (OperationCanceledException) when (attempt < maxAttempts)
            {
                // 超时且仍有重试机会，短暂等待后重试
                _logger.LogWarning("BatchStatusChanged 推送超时(attempt {Attempt}/{Max})，batchId={BatchId}, status={Status}",
                    attempt, maxAttempts, batchId, status);
                await Task.Delay(300 * attempt);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                // 连接异常且仍有重试机会
                _logger.LogWarning(ex, "BatchStatusChanged 推送失败(attempt {Attempt}/{Max})，batchId={BatchId}, status={Status}",
                    attempt, maxAttempts, batchId, status);
                await Task.Delay(300 * attempt);
            }
            catch (OperationCanceledException)
            {
                // 最终超时，记录日志后静默退出（前端可通过轮询 batch-sync 接口恢复）
                _logger.LogWarning("BatchStatusChanged 推送最终超时，batchId={BatchId}, status={Status}。前端将通过 batch-sync 轮询恢复",
                    batchId, status);
            }
            catch (Exception ex)
            {
                // 最终失败，记录日志后静默退出
                _logger.LogWarning(ex, "BatchStatusChanged 推送最终失败，batchId={BatchId}, status={Status}。前端将通过 batch-sync 轮询恢复",
                    batchId, status);
            }
        }
    }

    public async Task NotifyHomeStatsUpdatedAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group("home-stats")
                .SendAsync("OnHomeStatsUpdated", new { timestamp = DateTime.UtcNow }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyPipelineStageAsync(long batchId, string stageName, string status, string? message = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            // 前端监听 PipelineProgress，期望 {batchId, currentPhase, progressPercent?, processedRows?}
            var currentPhase = string.IsNullOrEmpty(message) ? $"{stageName}: {status}" : $"{stageName}: {message}";
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("PipelineProgress", new { batchId, currentPhase }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyImportLogAsync(long batchId, string level, string message)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("OnImportLog", new { batchId, level, message, timestamp = DateTime.Now }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyDingTalkSyncProgressAsync(object progress)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group("dingtalk-sync")
                .SendAsync("OnDingTalkSyncProgress", progress, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    // IDingTalkSyncProgressNotifier implementation
    public async Task NotifyProgressAsync(string stage, string message, int current, int total, int percent, object? result = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group("dingtalk-sync")
                .SendAsync("OnDingTalkSyncProgress", new { stage, message, current, total, percent, result }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyQualityAnalysisAsync(long batchId, int totalChecked, int passCount, int failCount, int dispatchedCount)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("OnQualityAnalysis", new
                {
                    BatchId = batchId,
                    TotalChecked = totalChecked,
                    PassCount = passCount,
                    FailCount = failCount,
                    DispatchedCount = dispatchedCount,
                    ErrorRate = totalChecked > 0 ? (double)failCount / totalChecked : 0,
                    Timestamp = DateTime.Now
                }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyExceptionDispatchedAsync(long batchId, string stageName, string errorMessage, int dispatchCount)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group("home-stats")
                .SendAsync("OnExceptionDispatched", new
                {
                    BatchId = batchId,
                    StageName = stageName,
                    ErrorMessage = errorMessage,
                    DispatchCount = dispatchCount,
                    Timestamp = DateTime.Now
                }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyPostPipelineStageAsync(long batchId, string stage, string status, string? message = null, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("OnPostPipelineStage", new { batchId, stage, status, message }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyDispatchItemAsync(long batchId, long dispatchResultId, string ruleName, string handlerType, int status, string? message = null, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("OnDispatchItemStatus", new { batchId, dispatchResultId, ruleName, handlerType, status, message }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyPostPipelineCompletedAsync(long batchId, int totalRules, int successCount, int failCount, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("OnPostPipelineCompleted", new { batchId, totalRules, successCount, failCount }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyAutoPluginStartedAsync(long batchId, string pluginName, int pluginIndex, int totalAutoPlugins = 0, List<AutoPluginStepDefinition>? steps = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("OnAutoPluginStarted", new { batchId, pluginName, pluginIndex, totalAutoPlugins, steps, timestamp = DateTime.Now }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyAutoPluginCompletedAsync(long batchId, string pluginName, int pluginIndex, bool success, string? message)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("OnAutoPluginCompleted", new { batchId, pluginName, pluginIndex, success, message, timestamp = DateTime.Now }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyBatchRollbackAsync(long batchId, int targetPluginIndex, List<string> rolledBackAutoPlugins)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("OnBatchRollback", new { batchId, targetPluginIndex, rolledBackAutoPlugins, timestamp = DateTime.Now }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断执行
        }
    }

    public async Task NotifyAutoPluginStepAsync(long batchId, string pluginName, int stepIndex, int totalSteps, string stepName, string status)
    {
        // 持久化当前节点名称到 CfBatch
        if (status == "Running" || status == "Completed")
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
                var batch = await context.Set<CfBatch>().AsTracking().FirstOrDefaultAsync(b => b.FID == batchId);
                if (batch != null)
                {
                    batch.FCurrentNodeName = status == "Completed" ? null : pluginName;
                    await context.SaveChangesAsync();
                }
            }
            catch
            {
                // 持久化失败不应中断 AutoPlugin 执行
            }
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("OnAutoPluginStep", new { batchId, pluginName, stepIndex, totalSteps, stepName, status, timestamp = DateTime.Now }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 超时但不中断 AutoPlugin 执行
        }
        catch (Exception)
        {
            // 其他异常也不应中断 AutoPlugin 执行
        }
    }

    public async Task NotifyAutoPluginDataProgressAsync(long batchId, string pluginName, int processedCount, int totalCount, string? detail = null)
    {
        var percent = totalCount > 0 ? (int)((double)processedCount / totalCount * 100) : 0;
        var key = $"{batchId}_{pluginName}";

        if (!ShouldReport(key, percent, processedCount == totalCount, processedCount == 1))
            return;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _hubContext.Clients.Group($"import-{batchId}")
                .SendAsync("OnAutoPluginDataProgress", new
                {
                    batchId,
                    pluginName,
                    processedCount,
                    totalCount,
                    percent,
                    detail
                }, cts.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "推送数据处理进度失败: batch={BatchId}", batchId);
        }
    }

    /// <summary>
    /// 节流判断：百分比变化>=5% 或 开始/完成时推送
    /// </summary>
    private static bool ShouldReport(string key, int percent, bool isCompleted, bool isFirst)
    {
        if (isFirst || isCompleted)
        {
            _lastReportedPercent[key] = percent;
            return true;
        }

        var lastPercent = _lastReportedPercent.GetOrAdd(key, -1);
        if (percent - lastPercent >= 5)
        {
            _lastReportedPercent[key] = percent;
            return true;
        }

        return false;
    }
}
