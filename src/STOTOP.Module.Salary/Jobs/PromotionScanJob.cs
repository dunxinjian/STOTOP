using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Salary.Entities;
using STOTOP.Module.Salary.Events;

namespace STOTOP.Module.Salary.Jobs;

/// <summary>
/// 晋升扫描 Job。每月 1 日 08:00 执行（RecurringJobId: sal.promotion-scan）。
///
/// 业务流程：
/// 1. 查询所有启用的 PromotionRule。
/// 2. 对每个规则，查找当前档位匹配的员工（SalaryArchive.F档位ID == rule.F当前档位ID）。
/// 3. 对每个匹配员工，通过 raw SQL 查询 PM积分账户 表获取 A 分余额。
/// 4. 判断 A 分 >= 规则阈值 → 创建 PromotionReview（待评审）→ 发布 PromotionTriggeredEvent。
/// 5. 单员工 try-catch 隔离，不中断整体。
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 3600)]
[AutomaticRetry(Attempts = 0)]
public class PromotionScanJob
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<PromotionScanJob> _logger;
    private readonly IEventDispatcher _eventDispatcher;

    public PromotionScanJob(
        STOTOPDbContext db,
        ILogger<PromotionScanJob> logger,
        IEventDispatcher eventDispatcher)
    {
        _db = db;
        _logger = logger;
        _eventDispatcher = eventDispatcher;
    }

    /// <summary>
    /// 晋升扫描入口。
    /// </summary>
    /// <param name="specificEmployeeId">仅扫描指定员工（手动触发场景），不传则全量</param>
    public async Task Execute(long? specificEmployeeId = null)
    {
        _logger.LogInformation("PromotionScanJob 启动 | specificEmployee={Employee}",
            specificEmployeeId?.ToString() ?? "<all>");

        // 1. 查询所有启用的晋升规则
        var rules = await _db.Set<PromotionRule>()
            .Where(r => r.F启用状态)
            .ToListAsync();

        if (rules.Count == 0)
        {
            _logger.LogInformation("PromotionScanJob 无启用的晋升规则，跳过");
            return;
        }

        // 预加载档位名称映射（用于事件发布）
        var gradeIds = rules.SelectMany(r => new[] { r.F当前档位ID, r.F目标档位ID }).Distinct().ToList();
        var gradeMap = await _db.Set<SalaryGrade>()
            .Where(g => gradeIds.Contains(g.FID))
            .ToDictionaryAsync(g => g.FID, g => g.F档位名称);

        int totalScanned = 0, totalTriggered = 0, totalFailed = 0;

        foreach (var rule in rules)
        {
            // 2. 查找当前档位匹配的员工
            var archiveQuery = _db.Set<SalaryArchive>()
                .Where(a => a.F档位ID == rule.F当前档位ID && a.FOrgId == rule.FOrgId);

            if (specificEmployeeId.HasValue)
            {
                archiveQuery = archiveQuery.Where(a => a.F员工ID == specificEmployeeId.Value);
            }

            var archives = await archiveQuery.ToListAsync();

            foreach (var archive in archives)
            {
                totalScanned++;
                try
                {
                    await ProcessEmployee(rule, archive, gradeMap);
                    totalTriggered++;
                }
                catch (Exception ex)
                {
                    totalFailed++;
                    _logger.LogError(ex,
                        "PromotionScanJob 单员工处理失败 | ruleId={RuleId} | employeeId={EmployeeId}",
                        rule.FID, archive.F员工ID);
                }
            }
        }

        _logger.LogInformation(
            "PromotionScanJob 完成 | rules={RuleCount} | scanned={Scanned} | triggered={Triggered} | failed={Failed}",
            rules.Count, totalScanned, totalTriggered, totalFailed);
    }

    private async Task ProcessEmployee(PromotionRule rule, SalaryArchive archive, Dictionary<long, string> gradeMap)
    {
        var employeeId = archive.F员工ID;
        var orgId = archive.FOrgId;

        // 3a. 通过 raw SQL 查询 PM积分账户 A 分余额（避免跨模块引用）
        var aScore = await _db.Database.SqlQueryRaw<int?>(
            "SELECT TOP 1 F当前积分 AS [Value] FROM PM积分账户 WHERE F用户ID = {0} AND F账户类型 = 1 AND F组织ID = {1}",
            employeeId, orgId).FirstOrDefaultAsync() ?? 0;

        // 3b. 判断 A 分是否达标
        if (aScore < rule.FA分阈值)
            return;

        // 3c. 检查是否已有待审评审单（避免重复创建）
        var existingReview = await _db.Set<PromotionReview>()
            .AnyAsync(r => r.F员工ID == employeeId && r.F规则ID == rule.FID && r.F状态 == 0);
        if (existingReview) return;

        // 3d. 创建 PromotionReview（F状态=0 待评审）
        var review = new PromotionReview
        {
            FOrgId = orgId,
            F员工ID = employeeId,
            F规则ID = rule.FID,
            F当前档位ID = rule.F当前档位ID,
            F目标档位ID = rule.F目标档位ID,
            F触发时间 = DateTime.Now,
            FA分快照 = aScore,
            F状态 = 0
        };
        _db.Set<PromotionReview>().Add(review);
        await _db.SaveChangesAsync();

        // 3e. 发布 PromotionTriggeredEvent
        try
        {
            await _eventDispatcher.PublishAsync(new PromotionTriggeredEvent
            {
                OrgId = orgId,
                ModuleCode = "Salary",
                EmployeeId = employeeId,
                ReviewId = review.FID,
                RuleId = rule.FID,
                CurrentGrade = gradeMap.GetValueOrDefault(rule.F当前档位ID, ""),
                TargetGrade = gradeMap.GetValueOrDefault(rule.F目标档位ID, ""),
                AScoreSnapshot = aScore
            });
        }
        catch (Exception ex)
        {
            // 事件发布失败不影响主流程
            _logger.LogWarning(ex,
                "PromotionTriggeredEvent 发布失败 | reviewId={ReviewId} | employeeId={EmployeeId}",
                review.FID, employeeId);
        }

        _logger.LogInformation(
            "PromotionScanJob 触发晋升 | employeeId={EmployeeId} | ruleId={RuleId} | aScore={AScore} | threshold={Threshold}",
            employeeId, rule.FID, aScore, rule.FA分阈值);
    }
}
