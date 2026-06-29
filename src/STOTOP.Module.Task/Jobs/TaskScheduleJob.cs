using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.Task.Services;

namespace STOTOP.Module.Task.Jobs;

/// <summary>
/// 任务调度定时Job - 每分钟检查到期调度并执行
/// </summary>
[AutomaticRetry(Attempts = 3)]
public class TaskScheduleJob
{
    private readonly STOTOPDbContext _db;
    private readonly ITaskScheduleService _scheduleService;
    private readonly ILogger<TaskScheduleJob> _logger;

    public TaskScheduleJob(
        STOTOPDbContext db,
        ITaskScheduleService scheduleService,
        ILogger<TaskScheduleJob> logger)
    {
        _db = db;
        _scheduleService = scheduleService;
        _logger = logger;
    }

    /// <summary>
    /// Hangfire RecurringJob 入口 - 每分钟执行
    /// </summary>
    public async global::System.Threading.Tasks.Task ExecuteAsync()
    {
        var now = DateTime.Now;

        var dueSchedules = await _db.Set<TmTaskSchedule>()
            .Where(s => s.FIsEnabled && s.FNextExecution != null && s.FNextExecution <= now)
            .Select(s => s.FID)
            .ToListAsync();

        if (dueSchedules.Count == 0)
            return;

        _logger.LogInformation("发现 {Count} 个到期调度，开始执行", dueSchedules.Count);

        foreach (var scheduleId in dueSchedules)
        {
            try
            {
                await _scheduleService.ExecuteScheduleAsync(scheduleId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行调度 {ScheduleId} 失败", scheduleId);
            }
        }

        _logger.LogInformation("调度Job执行完毕，共处理 {Count} 个调度", dueSchedules.Count);
    }
}
