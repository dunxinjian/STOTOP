using Hangfire;
using Microsoft.Extensions.Logging;
using STOTOP.Module.Task.Services.DingTalk;

namespace STOTOP.Module.Task.Jobs;

/// <summary>
/// 钉钉待办同步作业（Hangfire RecurringJob，每5分钟执行一次）
/// 同步待处理的钉钉待办记录
/// </summary>
public class DingTalkSyncJob
{
    private readonly IDingTalkTodoService _todoService;
    private readonly ILogger<DingTalkSyncJob> _logger;

    public DingTalkSyncJob(
        IDingTalkTodoService todoService,
        ILogger<DingTalkSyncJob> logger)
    {
        _todoService = todoService;
        _logger = logger;
    }

    /// <summary>
    /// 执行钉钉待办同步
    /// Cron: */5 * * * * （每5分钟）
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async global::System.Threading.Tasks.Task ExecuteAsync()
    {
        _logger.LogDebug("开始执行钉钉待办同步作业...");

        try
        {
            await _todoService.SyncPendingTodosAsync();
            _logger.LogDebug("钉钉待办同步作业执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "钉钉待办同步作业执行失败");
            throw; // 抛出异常让 Hangfire 自动重试
        }
    }
}
