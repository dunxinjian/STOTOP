using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Jobs;

/// <summary>
/// 管理层配额初始化Job：每月1日0点执行
/// 1. 将上月未完成的配额标记为已过期
/// 2. 为有配额资格的管理层创建新月配额
/// </summary>
[AutomaticRetry(Attempts = 3)]
public class ManagerQuotaInitJob
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<ManagerQuotaInitJob> _logger;

    public ManagerQuotaInitJob(STOTOPDbContext db, ILogger<ManagerQuotaInitJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 执行配额初始化：过期旧配额 + 创建新月配额
    /// </summary>
    public async global::System.Threading.Tasks.Task ExecuteAsync()
    {
        _logger.LogInformation("ManagerQuotaInitJob 开始执行...");

        // 1. 将上月未完成的配额标记为已过期（FStatus: 0=正常, 1=已过期）
        var lastMonth = DateTime.Now.AddMonths(-1).ToString("yyyy-MM");
        var expiredQuotas = await _db.Set<PmManagerQuota>()
            .Where(q => q.FYearMonth == lastMonth && q.FStatus == 0)
            .ToListAsync();

        foreach (var quota in expiredQuotas)
        {
            quota.FStatus = 1; // 标记为已过期
        }

        if (expiredQuotas.Count > 0)
        {
            _logger.LogInformation("ManagerQuotaInitJob 标记 {Count} 条上月配额为已过期", expiredQuotas.Count);
        }

        // 2. 为有配额资格的管理层创建新月配额
        // 查找上月有配额记录的管理者（作为资格依据）
        var currentMonth = DateTime.Now.ToString("yyyy-MM");
        var existingManagers = await _db.Set<PmManagerQuota>()
            .Where(q => q.FYearMonth == currentMonth)
            .Select(q => q.FManagerId)
            .ToListAsync();

        var qualifiedManagers = await _db.Set<PmManagerQuota>()
            .Where(q => q.FYearMonth == lastMonth)
            .Where(q => !existingManagers.Contains(q.FManagerId))
            .ToListAsync();

        var newQuotaCount = 0;
        foreach (var prevQuota in qualifiedManagers)
        {
            var newQuota = new PmManagerQuota
            {
                FOrgId = prevQuota.FOrgId,
                FManagerId = prevQuota.FManagerId,
                FYearMonth = currentMonth,
                FAwardQuota = prevQuota.FAwardQuota,
                FDeductQuota = prevQuota.FDeductQuota,
                FUsedAward = 0,
                FUsedDeduct = 0,
                FStatus = 0,
                FCreateTime = DateTime.Now
            };
            _db.Set<PmManagerQuota>().Add(newQuota);
            newQuotaCount++;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("ManagerQuotaInitJob 执行完成，过期 {ExpiredCount} 条，新建 {NewCount} 条配额",
            expiredQuotas.Count, newQuotaCount);
    }
}
