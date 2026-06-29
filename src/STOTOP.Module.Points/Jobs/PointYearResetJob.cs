using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Points.Constants;
using STOTOP.Module.Points.Entities;
using STOTOP.Module.Points.Events;
using STOTOP.Module.Points.Services;

namespace STOTOP.Module.Points.Jobs;

/// <summary>
/// 年清 Job：每年 1 月 1 日 02:30 执行（Cron: 30 2 1 1 *）。
///
/// 业务流程（以"上一年"为清算期间）：
/// 1. 计算 atDate = 上一年最后一日 23:59:59.999；periodKey = atDate 所在年（yyyy）。
/// 2. 遍历所有组织 → 该组织下所有 B 分账户。
/// 3. 单账户事务内：取半开区间余额 → 写快照推进 → 命中年清规则按策略归零/转券/自定义 → 写 PmPointResetRecord（清算类型=2）。
/// 4. 事务外发布 PointResetEvent。
/// 5. 单条异常隔离。
///
/// 与 PointMonthResetJob 结构对称，差异仅在：周期键、清算类型常量、规则的 EventType="YearReset"。
/// </summary>
[AutomaticRetry(Attempts = 3)]
public class PointYearResetJob
{
    private readonly STOTOPDbContext _db;
    private readonly IPointService _pointService;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<PointYearResetJob> _logger;

    public PointYearResetJob(
        STOTOPDbContext db,
        IPointService pointService,
        IEventDispatcher eventDispatcher,
        ILogger<PointYearResetJob> logger)
    {
        _db = db;
        _pointService = pointService;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public async global::System.Threading.Tasks.Task ExecuteAsync()
    {
        var now = DateTime.Now;
        var thisYearFirst = new DateTime(now.Year, 1, 1);
        var atDate = thisYearFirst.AddTicks(-1);                    // 上一年最后一刻
        var lastYearFirst = thisYearFirst.AddYears(-1);
        var periodKey = lastYearFirst.ToString("yyyy");

        _logger.LogInformation("PointYearResetJob 启动 | period={Period} | atDate={AtDate:O}", periodKey, atDate);

        var orgIds = await _db.Set<PmPointAccount>()
            .Where(a => a.F账户类型 == PointAccountTypes.B)
            .Select(a => a.FOrgId)
            .Distinct()
            .ToListAsync();

        int totalProcessed = 0, totalSucceeded = 0, totalFailed = 0;

        foreach (var orgId in orgIds)
        {
            var result = await ProcessOrgAsync(orgId, atDate, periodKey);
            totalProcessed += result.processed;
            totalSucceeded += result.succeeded;
            totalFailed += result.failed;
        }

        _logger.LogInformation(
            "PointYearResetJob 完成 | period={Period} | orgs={OrgCount} | processed={Processed} | succeeded={Succeeded} | failed={Failed}",
            periodKey, orgIds.Count, totalProcessed, totalSucceeded, totalFailed);
    }

    private async Task<(int processed, int succeeded, int failed)> ProcessOrgAsync(long orgId, DateTime atDate, string periodKey)
    {
        var accounts = await _db.Set<PmPointAccount>()
            .Where(a => a.FOrgId == orgId && a.F账户类型 == PointAccountTypes.B)
            .ToListAsync();

        var rule = await _db.Set<PmPointRule>()
            .Where(r => r.FOrgId == orgId
                && r.FIsEnabled
                && r.F账户类型 == PointAccountTypes.B
                && r.FEventType == "YearReset")
            .OrderBy(r => r.FSortOrder)
            .FirstOrDefaultAsync();

        var strategy = rule?.F清算策略 ?? 0;
        var ratio = rule?.F转换比例 ?? 1.0m;

        int processed = 0, succeeded = 0, failed = 0;
        foreach (var account in accounts)
        {
            processed++;
            try
            {
                await ResetSingleAccountAsync(account, atDate, periodKey, strategy, ratio, resetType: 2);
                succeeded++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "PointYearResetJob 单账户清算失败 | orgId={OrgId} | userId={UserId} | period={Period}",
                    account.FOrgId, account.FUserId, periodKey);
            }
        }
        return (processed, succeeded, failed);
    }

    private async global::System.Threading.Tasks.Task ResetSingleAccountAsync(
        PmPointAccount account, DateTime atDate, string periodKey, int strategy, decimal ratio, int resetType)
    {
        var orgId = account.FOrgId;
        var userId = account.FUserId;
        var accountType = account.F账户类型;

        var existed = await _db.Set<PmPointResetRecord>()
            .AnyAsync(r => r.FOrgId == orgId
                && r.F员工ID == userId
                && r.F账户类型 == accountType
                && r.F清算期间 == periodKey
                && r.F清算类型 == resetType);
        if (existed)
        {
            _logger.LogInformation("PointYearResetJob 跳过已清算 | orgId={OrgId} | userId={UserId} | period={Period}", orgId, userId, periodKey);
            return;
        }

        var balanceResult = await _pointService.GetAccountBalanceAtDateAsync(orgId, userId, accountType, atDate);
        if (balanceResult.Code != 200)
            throw new InvalidOperationException($"GetAccountBalanceAtDateAsync 失败：{balanceResult.Message}");
        var beforeBalance = balanceResult.Data;

        decimal voucherValue = 0m;
        int afterBalance;
        string remark;

        switch (strategy)
        {
            case 0:
                afterBalance = 0;
                remark = "年清-归零";
                break;
            case 1:
                voucherValue = beforeBalance * ratio;
                afterBalance = 0;
                remark = $"年清-转福利券 ratio={ratio} value={voucherValue}（券发放由福利券模块接入后落地）";
                _logger.LogInformation(
                    "PointYearResetJob 转福利券（占位）| orgId={OrgId} | userId={UserId} | balance={Balance} | ratio={Ratio} | value={Value}",
                    orgId, userId, beforeBalance, ratio, voucherValue);
                break;
            case 2:
                afterBalance = beforeBalance;
                voucherValue = 0m;
                remark = "年清-自定义策略（占位未执行）";
                _logger.LogWarning(
                    "PointYearResetJob 命中自定义清算策略，未执行余额变更 | orgId={OrgId} | userId={UserId}",
                    orgId, userId);
                break;
            default:
                afterBalance = 0;
                remark = $"年清-未知策略({strategy}) 按归零处理";
                _logger.LogWarning("PointYearResetJob 未知清算策略 strategy={Strategy} 按归零处理", strategy);
                break;
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            account.F期初余额快照日期 = atDate;
            account.F期初余额快照值 = afterBalance;

            if (strategy == 0 || strategy == 1)
            {
                // 年清同时归零年度积分计数器（B 分账户）
                account.FAvailablePoints = 0;
                account.FMonthlyAward = 0;
                account.FMonthlyDeduct = 0;
                account.FYearlyPoints = 0;
            }
            account.FUpdateTime = DateTime.Now;

            var resetRecord = new PmPointResetRecord
            {
                FOrgId = orgId,
                F员工ID = userId,
                F账户类型 = accountType,
                F清算期间 = periodKey,
                F清算类型 = resetType,
                F清算策略 = strategy,
                F清算前余额 = beforeBalance,
                F清算后余额 = afterBalance,
                F转换比例 = ratio,
                F兑换福利券值 = voucherValue,
                F关联兑换记录ID = null,
                F快照日期 = atDate,
                F执行时间 = DateTime.Now,
                F备注 = remark
            };
            _db.Set<PmPointResetRecord>().Add(resetRecord);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        try
        {
            await _eventDispatcher.PublishAsync(new PointResetEvent
            {
                OrgId = orgId,
                UserId = userId,
                AccountType = accountType,
                ResetType = resetType,
                BalanceBeforeReset = beforeBalance,
                BalanceAfterReset = afterBalance,
                ConvertedToVoucherValue = voucherValue,
                PeriodKey = periodKey,
                TriggeredByUserId = 0,
                ModuleCode = "points"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PointYearResetJob 发布 PointResetEvent 失败 | orgId={OrgId} | userId={UserId}", orgId, userId);
        }
    }
}
