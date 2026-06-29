using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Download;

/// <summary>
/// Hangfire 任务调度服务
/// </summary>
public class DownloadJobService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<DownloadJobService> _logger;

    public DownloadJobService(
        STOTOPDbContext context,
        ILogger<DownloadJobService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 创建/更新定时任务
    /// </summary>
    public async Task ScheduleTaskAsync(long taskId)
    {
        var task = await _context.Set<CfDownloadTask>()
            .FirstOrDefaultAsync(t => t.FID == taskId);

        if (task == null)
        {
            _logger.LogWarning("下载任务 {TaskId} 不存在，无法注册定时任务", taskId);
            return;
        }

        if (string.IsNullOrWhiteSpace(task.FCronExpression))
        {
            _logger.LogInformation("下载任务 [{TaskName}] 未配置 Cron 表达式，跳过定时注册", task.FTaskName);
            // 如果之前有定时任务，取消掉
            if (!string.IsNullOrWhiteSpace(task.FHangfireJobId))
            {
                RecurringJob.RemoveIfExists(task.FHangfireJobId);
                task.FHangfireJobId = null;
                await _context.SaveChangesAsync();
            }
            return;
        }

        var jobId = $"download-task-{taskId}";

        RecurringJob.AddOrUpdate<DownloadEngineService>(
            jobId,
            engine => engine.ExecuteDownloadAsync(taskId, CancellationToken.None),
            task.FCronExpression);

        task.FHangfireJobId = jobId;
        await _context.SaveChangesAsync();

        _logger.LogInformation("已注册定时任务 [{TaskName}]，JobId={JobId}，Cron={Cron}",
            task.FTaskName, jobId, task.FCronExpression);
    }

    /// <summary>
    /// 删除定时任务
    /// </summary>
    public async Task UnscheduleTaskAsync(long taskId)
    {
        var task = await _context.Set<CfDownloadTask>()
            .FirstOrDefaultAsync(t => t.FID == taskId);

        if (task == null) return;

        if (!string.IsNullOrWhiteSpace(task.FHangfireJobId))
        {
            RecurringJob.RemoveIfExists(task.FHangfireJobId);
            _logger.LogInformation("已删除定时任务 [{TaskName}]，JobId={JobId}",
                task.FTaskName, task.FHangfireJobId);
            task.FHangfireJobId = null;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 立即执行一次
    /// </summary>
    public async Task TriggerOnceAsync(long taskId)
    {
        var task = await _context.Set<CfDownloadTask>()
            .FirstOrDefaultAsync(t => t.FID == taskId);

        if (task == null)
        {
            _logger.LogWarning("下载任务 {TaskId} 不存在，无法触发执行", taskId);
            return;
        }

        BackgroundJob.Enqueue<DownloadEngineService>(
            engine => engine.ExecuteDownloadAsync(taskId, CancellationToken.None));

        _logger.LogInformation("已触发下载任务 [{TaskName}] 立即执行一次", task.FTaskName);
    }
}
