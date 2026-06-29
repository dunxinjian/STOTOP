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
/// 月清 Job：每月 1 日 02:00 执行（Cron: 0 2 1 * *）。
///
/// 业务流程（以"上月"为清算期间）：
/// 1. 计算 atDate = 上月最后一日 23:59:59.999；periodKey = atDate 所在月（yyyy-MM）。
/// 2. 遍历所有组织 → 该组织下所有 B 分账户（PmPointAccount.F账户类型 = B = 2）。
/// 3. 单账户事务内：
///    a. 调 IPointService.GetAccountBalanceAtDateAsync 计算 atDate 的余额（半开区间）。
///    b. 写入 PmPointAccount.F期初余额快照日期 = atDate / F期初余额快照值 = 余额（推进半开区间起点）。
///    c. 查询 PmPointRule（命中 EventType=MonthReset & 账户类型=B）；若无规则按"归零"默认。
///    d. 按 F清算策略 分支：
///       0=归零：account.FAvailablePoints/FMonthlyAward/FMonthlyDeduct 清零；
///       1=转福利券：兑换面值 = balance × F转换比例（占位写日志，不真实建券，由后续任务接入福利券模块）；同时清零；
///       2=自定义：仅 LogWarning 占位，不做余额变更。
///    e. 写入 PmPointResetRecord 留痕（唯一约束保证 Job 重跑幂等：唯一键冲突会被忽略并记日志）。
///    f. 提交事务后发布 PointResetEvent。
/// 4. 单条异常隔离：catch 后日志记录，不终止整批。
/// </summary>
[AutomaticRetry(Attempts = 3)]
public class PointMonthResetJob
{
    private readonly STOTOPDbContext _db;
    private readonly IPointService _pointService;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<PointMonthResetJob> _logger;

    public PointMonthResetJob(
        STOTOPDbContext db,
        IPointService pointService,
        IEventDispatcher eventDispatcher,
        ILogger<PointMonthResetJob> logger)
    {
        _db = db;
        _pointService = pointService;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public async global::System.Threading.Tasks.Task ExecuteAsync()
    {
        // 计算上月最后一日 23:59:59.999 作为半开区间右端点
        var now = DateTime.Now;
        var thisMonthFirst = new DateTime(now.Year, now.Month, 1);
        var atDate = thisMonthFirst.AddTicks(-1);                       // 上月最后一刻
        var lastMonthFirst = thisMonthFirst.AddMonths(-1);
        var periodKey = lastMonthFirst.ToString("yyyy-MM");

        _logger.LogInformation("PointMonthResetJob 启动 | period={Period} | atDate={AtDate:O}", periodKey, atDate);

        // 取所有 B 分账户的 OrgId（清算只作用 B 分）
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
            "PointMonthResetJob 完成 | period={Period} | orgs={OrgCount} | processed={Processed} | succeeded={Succeeded} | failed={Failed}",
            periodKey, orgIds.Count, totalProcessed, totalSucceeded, totalFailed);
    }

    private async Task<(int processed, int succeeded, int failed)> ProcessOrgAsync(long orgId, DateTime atDate, string periodKey)
    {
        var accounts = await _db.Set<PmPointAccount>()
            .Where(a => a.FOrgId == orgId && a.F账户类型 == PointAccountTypes.B)
            .ToListAsync();

        // 该组织下命中 MonthReset 的 B 分规则（取启用中、SortOrder 最小者作为生效规则）
        var rule = await _db.Set<PmPointRule>()
            .Where(r => r.FOrgId == orgId
                && r.FIsEnabled
                && r.F账户类型 == PointAccountTypes.B
                && r.FEventType == "MonthReset")
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
                await ResetSingleAccountAsync(account, atDate, periodKey, strategy, ratio, resetType: 1);
                succeeded++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "PointMonthResetJob 单账户清算失败 | orgId={OrgId} | userId={UserId} | period={Period}",
                    account.FOrgId, account.FUserId, periodKey);
            }
        }
        return (processed, succeeded, failed);
    }

    private async global::System.Threading.Tasks.Task ResetSingleAccountAsync(
        PmPointAccount account, DateTime atDate, string periodKey, int strategy, decimal ratio, int resetType)
    {
        // 唯一键预查（避免重跑撞唯一索引报错被升级到 retry）
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
            _logger.LogInformation("PointMonthResetJob 跳过已清算 | orgId={OrgId} | userId={UserId} | period={Period}", orgId, userId, periodKey);
            return;
        }

        // 半开区间余额
        var balanceResult = await _pointService.GetAccountBalanceAtDateAsync(orgId, userId, accountType, atDate);
        if (balanceResult.Code != 200)
            throw new InvalidOperationException($"GetAccountBalanceAtDateAsync 失败：{balanceResult.Message}");
        var beforeBalance = balanceResult.Data;
        decimal voucherValue = 0m;
        int afterBalance;
        string remark;

        switch (strategy)
        {
            case 0: // 归零
                afterBalance = 0;
                remark = "月清-归零";
                break;
            case 1: // 转福利券
                voucherValue = beforeBalance * ratio;
                afterBalance = 0;
                remark = $"月清-转福利券 ratio={ratio} value={voucherValue}（券发放由福利券模块接入后落地）";
                _logger.LogInformation(
                    "PointMonthResetJob 转福利券（占位）| orgId={OrgId} | userId={UserId} | balance={Balance} | ratio={Ratio} | value={Value}",
                    orgId, userId, beforeBalance, ratio, voucherValue);
                break;
            case 2: // 自定义
                afterBalance = beforeBalance;
                voucherValue = 0m;
                remark = "月清-自定义策略（占位未执行）";
                _logger.LogWarning(
                    "PointMonthResetJob 命中自定义清算策略，未执行余额变更 | orgId={OrgId} | userId={UserId}",
                    orgId, userId);
                break;
            default:
                afterBalance = 0;
                remark = $"月清-未知策略({strategy}) 按归零处理";
                _logger.LogWarning("PointMonthResetJob 未知清算策略 strategy={Strategy} 按归零处理", strategy);
                break;
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. 推进快照日期 + 写入快照值（半开区间起点前移到 atDate）
            account.F期初余额快照日期 = atDate;
            account.F期初余额快照值 = afterBalance;

            // 2. 余额清零（仅在策略 0/1 时）
            if (strategy == 0 || strategy == 1)
            {
                account.FAvailablePoints = 0;
                account.FMonthlyAward = 0;
                account.FMonthlyDeduct = 0;
            }
            account.FUpdateTime = DateTime.Now;

            // 3. 留痕
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

        // 4. 事务外发布事件（事件失败不影响清算）
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
            _logger.LogWarning(ex, "PointMonthResetJob 发布 PointResetEvent 失败 | orgId={OrgId} | userId={UserId}", orgId, userId);
        }
    }
}
