using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.Task.Services;

namespace STOTOP.Module.Task.Jobs;

/// <summary>
/// 绩效自动汇算定时作业
/// 查询状态为「进行中」且已到截止日期的考核周期，自动触发汇算
/// </summary>
public class PerformanceCalcJob
{
    private readonly IPerformanceService _performanceService;
    private readonly STOTOPDbContext _db;
    private readonly ILogger<PerformanceCalcJob> _logger;

    public PerformanceCalcJob(
        IPerformanceService performanceService,
        STOTOPDbContext db,
        ILogger<PerformanceCalcJob> logger)
    {
        _performanceService = performanceService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 执行绩效汇算：查询已到截止日期的进行中周期，逐一触发汇算
    /// Hangfire RecurringJob，建议每天凌晨执行一次
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async global::System.Threading.Tasks.Task ExecuteAsync()
    {
        _logger.LogInformation("绩效汇算定时任务开始执行...");

        try
        {
            // 查询状态为「进行中(1)」且已到截止日期的考核周期
            var periods = await _db.Set<TmPerformancePeriod>()
                .Where(p => p.FStatus == 1 && p.FEndDate <= DateTime.Now.Date)
                .ToListAsync();

            if (!periods.Any())
            {
                _logger.LogInformation("无需汇算的考核周期");
                return;
            }

            _logger.LogInformation("发现 {Count} 个待汇算考核周期", periods.Count);

            foreach (var period in periods)
            {
                try
                {
                    _logger.LogInformation("开始汇算周期：{PeriodName} (ID={PeriodId})", period.FName, period.FID);

                    var result = await _performanceService.CalculateAsync(period.FID);

                    if (result.Code == 200)
                    {
                        _logger.LogInformation("周期 {PeriodName} 汇算完成", period.FName);
                    }
                    else
                    {
                        _logger.LogWarning("周期 {PeriodName} 汇算失败：{Message}", period.FName, result.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "周期 {PeriodName} (ID={PeriodId}) 汇算异常", period.FName, period.FID);
                    // 继续处理下一个周期，不中断整体流程
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "绩效汇算定时任务执行异常");
            throw; // 重新抛出以触发 Hangfire 重试
        }

        _logger.LogInformation("绩效汇算定时任务执行完毕");
    }
}
