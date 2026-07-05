using Microsoft.Extensions.Logging;
using STOTOP.Module.CardFlow.AutoPlugin;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// IBatchNotifier 实现：经 IProgressNotifier 推送到 ProgressHub 的 import-{batchId} 组，所有方法自带 try-catch。
/// 不可直推 CardFlowHub：该 Hub 无批次订阅入口、前端 useBatchSync 只监听 progress 连接，
/// 且 Clients.All 会跨组织广播。
/// </summary>
public class BatchNotifier : IBatchNotifier
{
    private readonly IProgressNotifier _progressNotifier;
    private readonly ILogger<BatchNotifier> _logger;

    public BatchNotifier(IProgressNotifier progressNotifier, ILogger<BatchNotifier> logger)
    {
        _progressNotifier = progressNotifier;
        _logger = logger;
    }

    public async Task PipelineStartedAsync(long batchId, IEnumerable<PluginSnapshot> plugins)
    {
        try
        {
            await _progressNotifier.NotifyBatchPipelineStartedAsync(batchId, plugins);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR推送BatchPipelineStarted失败, BatchId={BatchId}", batchId);
        }
    }

    public async Task PluginStatusChangedAsync(long batchId, int pluginIndex, string pluginName, string status, string? error = null)
    {
        try
        {
            await _progressNotifier.NotifyPluginStatusChangedAsync(batchId, pluginIndex, pluginName, status, error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR推送PluginStatusChanged失败, BatchId={BatchId}, Plugin={PluginName}", batchId, pluginName);
        }
    }

    public async Task ProgressUpdateAsync(long batchId, int processed, int total)
    {
        try
        {
            await _progressNotifier.NotifyBatchProgressUpdateAsync(batchId, processed, total);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR推送BatchProgressUpdate失败, BatchId={BatchId}", batchId);
        }
    }
}
