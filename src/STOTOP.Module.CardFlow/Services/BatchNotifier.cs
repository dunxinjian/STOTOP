using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using STOTOP.Module.CardFlow.Hubs;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>IBatchNotifier 实现：通过 SignalR 推送批次事件，所有方法自带 try-catch</summary>
public class BatchNotifier : IBatchNotifier
{
    private readonly IHubContext<CardFlowHub> _hubContext;
    private readonly ILogger<BatchNotifier> _logger;

    public BatchNotifier(IHubContext<CardFlowHub> hubContext, ILogger<BatchNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PipelineStartedAsync(long batchId, IEnumerable<PluginSnapshot> plugins)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("BatchPipelineStarted", new
            {
                batchId,
                plugins = plugins.Select(p => new { name = p.Name, index = p.Index, status = p.Status })
            });
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
            await _hubContext.Clients.All.SendAsync("PluginStatusChanged", new
            {
                batchId,
                pluginIndex,
                pluginName,
                status,
                error
            });
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
            await _hubContext.Clients.All.SendAsync("BatchProgressUpdate", new
            {
                batchId,
                processedRows = processed,
                totalRows = total
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR推送BatchProgressUpdate失败, BatchId={BatchId}", batchId);
        }
    }
}
