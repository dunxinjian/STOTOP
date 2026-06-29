using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// 实时进度通知接口（迁移自 CFAutoPlugin，原迁移自 DataCenter）
/// 通过 SignalR 推送批次/AutoPlugin/管道进度到前端
/// </summary>
public interface IProgressNotifier
{
    Task NotifyImportProgressAsync(long batchId, int processedRows, int totalRows, string stage);
    Task NotifyDownloadProgressAsync(long taskId, int currentStep, int totalSteps, string stepName);
    Task NotifyProcessingProgressAsync(long ruleId, int processed, int total, string status);
    Task NotifyBatchStatusChangedAsync(long batchId, int newStatus, string statusText);
    /// <summary>批次状态变更（含统计摘要）</summary>
    Task NotifyBatchStatusChangedAsync(long batchId, int newStatus, string statusText, BatchSummaryDto? summary);
    /// <summary>批次状态变更（含全局版本号，用于前端乖序判定）</summary>
    Task NotifyBatchStatusChangedAsync(long batchId, int newStatus, string statusText, BatchSummaryDto? summary, long version);
    Task NotifyHomeStatsUpdatedAsync();

    /// <summary>管道阶段状态通知</summary>
    Task NotifyPipelineStageAsync(long batchId, string stageName, string status, string? message = null);

    /// <summary>导入日志通知</summary>
    Task NotifyImportLogAsync(long batchId, string level, string message);

    /// <summary>钉钉同步进度通知</summary>
    Task NotifyDingTalkSyncProgressAsync(object progress);

    /// <summary>质量分析完成通知</summary>
    Task NotifyQualityAnalysisAsync(long batchId, int totalChecked, int passCount, int failCount, int dispatchedCount);

    /// <summary>Pipeline 阶段异常派发完成通知</summary>
    Task NotifyExceptionDispatchedAsync(long batchId, string stageName, string errorMessage, int dispatchCount);

    /// <summary>后处理链阶段状态推送</summary>
    Task NotifyPostPipelineStageAsync(long batchId, string stage, string status, string? message = null, CancellationToken ct = default);

    /// <summary>单条派发规则执行状态推送</summary>
    Task NotifyDispatchItemAsync(long batchId, long dispatchResultId, string ruleName, string handlerType, int status, string? message = null, CancellationToken ct = default);

    /// <summary>后处理链整体完成推送</summary>
    Task NotifyPostPipelineCompletedAsync(long batchId, int totalRules, int successCount, int failCount, CancellationToken ct = default);

    /// <summary>AutoPlugin 开始执行通知</summary>
    Task NotifyAutoPluginStartedAsync(long batchId, string pluginName, int pluginIndex, int totalAutoPlugins = 0, List<AutoPluginStepDefinition>? steps = null);

    /// <summary>AutoPlugin 执行完成通知</summary>
    Task NotifyAutoPluginCompletedAsync(long batchId, string pluginName, int pluginIndex, bool success, string? message);

    /// <summary>批次回撤通知</summary>
    Task NotifyBatchRollbackAsync(long batchId, int targetPluginIndex, List<string> rolledBackAutoPlugins);

    /// <summary>AutoPlugin 内部步骤进度通知</summary>
    Task NotifyAutoPluginStepAsync(long batchId, string pluginName, int stepIndex, int totalSteps, string stepName, string status);

    /// <summary>AutoPlugin 数据处理进度推送（用于循环内行级进度，内置节流）</summary>
    Task NotifyAutoPluginDataProgressAsync(long batchId, string pluginName, int processedCount, int totalCount, string? detail = null);
}

/// <summary>AutoPlugin 步骤定义</summary>
public class AutoPluginStepDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>批次统计摘要 DTO（迁移自 CFAutoPlugin，原迁移自 DataCenter）</summary>
public class BatchSummaryDto
{
    public int Success { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int Total { get; set; }
}
