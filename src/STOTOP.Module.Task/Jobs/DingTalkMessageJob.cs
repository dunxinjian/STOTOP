using Hangfire;
using Microsoft.Extensions.Logging;
using STOTOP.Module.Task.Services.DingTalk;

namespace STOTOP.Module.Task.Jobs;

/// <summary>
/// 钉钉消息推送作业（Hangfire RecurringJob，每分钟执行一次）
/// 处理待推送的钉钉消息
/// </summary>
public class DingTalkMessageJob
{
    private readonly IDingTalkMessageService _messageService;
    private readonly ILogger<DingTalkMessageJob> _logger;

    public DingTalkMessageJob(
        IDingTalkMessageService messageService,
        ILogger<DingTalkMessageJob> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    /// <summary>
    /// 执行钉钉消息推送
    /// Cron: * * * * * （每分钟）
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async global::System.Threading.Tasks.Task ExecuteAsync()
    {
        _logger.LogDebug("开始执行钉钉消息推送作业...");

        try
        {
            await _messageService.ProcessPendingMessagesAsync();
            _logger.LogDebug("钉钉消息推送作业执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "钉钉消息推送作业执行失败");
            throw; // 抛出异常让 Hangfire 自动重试
        }
    }
}
