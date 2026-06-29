using Hangfire;
using Microsoft.Extensions.Logging;
using STOTOP.Module.Task.Services;

namespace STOTOP.Module.Task.Jobs;

/// <summary>
/// 任务提醒定时Job - 每分钟检查到期提醒并处理
/// </summary>
[AutomaticRetry(Attempts = 3)]
public class TaskReminderJob
{
    private readonly ITaskReminderService _reminderService;
    private readonly ILogger<TaskReminderJob> _logger;

    public TaskReminderJob(
        ITaskReminderService reminderService,
        ILogger<TaskReminderJob> logger)
    {
        _reminderService = reminderService;
        _logger = logger;
    }

    /// <summary>
    /// Hangfire RecurringJob 入口 - 每分钟执行
    /// </summary>
    public async global::System.Threading.Tasks.Task ExecuteAsync()
    {
        try
        {
            await _reminderService.ProcessDueRemindersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理到期提醒失败");
            throw; // 抛出让Hangfire自动重试
        }
    }
}
