using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Contracts.Hr;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.PPV.Entities;
using STOTOP.Module.PPV.Events;

namespace STOTOP.Module.PPV.Jobs;

/// <summary>
/// PPV 月度汇总 Job。每月 6 日 04:00 执行（RecurringJobId: ppv.aggregate-monthly）。
///
/// 业务流程（以"上月"为核算期间，period = yyyyMM）：
/// 1. 查询该期间所有已审核通过的 PPV产值记录，按员工分组。
/// 2. 对每个员工执行 CalcForEmployee：
///    a. 通过 IEmployeeOrgQueryService 获取岗位/部门快照。
///    b. 计算本岗产值 = SUM(F产值金额) WHERE F是否跨岗=false
///    c. 计算跨岗产值 = SUM(F产值金额) WHERE F是否跨岗=true
///    d. 查询违规记录 → 跨岗清零判定。
///    e. 计算综合质量等级。
///    f. 计算 B 分 / A 分变化。
///    g. 幂等写入 PpvMonthlyResult。
/// 3. 单员工异常 try-catch 隔离，不中断整体。
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 3600)]
[AutomaticRetry(Attempts = 0)]
public class PpvCalcJob
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<PpvCalcJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventDispatcher _eventDispatcher;

    public PpvCalcJob(
        STOTOPDbContext db,
        ILogger<PpvCalcJob> logger,
        IServiceProvider serviceProvider,
        IEventDispatcher eventDispatcher)
    {
        _db = db;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _eventDispatcher = eventDispatcher;
    }

    /// <summary>
    /// 月度汇总入口。
    /// </summary>
    /// <param name="period">期间 yyyyMM，默认上月</param>
    /// <param name="specificEmployeeId">仅核算指定员工（手动重跑场景），不传则全量</param>
    public async Task Execute(string? period = null, long? specificEmployeeId = null)
    {
        period ??= DateTime.Now.AddMonths(-1).ToString("yyyyMM");
        var periodEndDate = GetPeriodEndDate(period);

        _logger.LogInformation("PpvCalcJob 启动 | period={Period} | specificEmployee={Employee}",
            period, specificEmployeeId?.ToString() ?? "<all>");

        // 1. 查询该期间所有已审核通过的 PPV 产值记录
        var query = _db.Set<PpvRecord>()
            .Where(r => r.F期间 == period && r.F审核状态 == 1);

        if (specificEmployeeId.HasValue)
        {
            query = query.Where(r => r.F员工ID == specificEmployeeId.Value);
        }

        var records = await query.ToListAsync();

        // 按员工分组
        var groupedByEmployee = records.GroupBy(r => new { r.F员工ID, r.FOrgId }).ToList();

        int totalEmployees = 0, totalSucceeded = 0, totalFailed = 0;

        foreach (var group in groupedByEmployee)
        {
            totalEmployees++;
            try
            {
                await CalcForEmployee(group.Key.FOrgId, group.Key.F员工ID, period, periodEndDate, group.ToList());
                totalSucceeded++;
                _logger.LogInformation(
                    "PpvCalcJob 员工汇总完成 | employeeId={EmployeeId} | orgId={OrgId} | period={Period}",
                    group.Key.F员工ID, group.Key.FOrgId, period);
            }
            catch (Exception ex)
            {
                totalFailed++;
                _logger.LogError(ex,
                    "PpvCalcJob 单员工汇总失败 | employeeId={EmployeeId} | orgId={OrgId} | period={Period}",
                    group.Key.F员工ID, group.Key.FOrgId, period);
            }
        }

        _logger.LogInformation(
            "PpvCalcJob 完成 | period={Period} | employees={EmpCount} | ok={Ok} | fail={Fail}",
            period, totalEmployees, totalSucceeded, totalFailed);
    }

    private async Task CalcForEmployee(long orgId, long employeeId, string period, DateTime periodEndDate, List<PpvRecord> employeeRecords)
    {
        // Step a: 获取岗位/部门快照
        long positionId = 0;
        long deptId = 0;

        try
        {
            var employeeOrgService = _serviceProvider.GetService<IEmployeeOrgQueryService>();
            if (employeeOrgService != null)
            {
                var snapshot = await employeeOrgService.GetSnapshotAsync(employeeId, orgId, periodEndDate);
                positionId = snapshot?.PrimaryPositionId ?? 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PpvCalcJob 获取员工快照失败，使用默认值 | employeeId={EmployeeId} | orgId={OrgId}",
                employeeId, orgId);
        }

        // Step b: 计算本岗产值
        var ownPositionValue = employeeRecords
            .Where(r => !r.F是否跨岗)
            .Sum(r => r.F产值金额);

        // Step c: 计算跨岗产值
        var crossPositionValue = employeeRecords
            .Where(r => r.F是否跨岗)
            .Sum(r => r.F产值金额);

        // Step d: 查询违规记录（已确认）
        var violations = await _db.Set<PpvViolation>()
            .Where(v => v.FOrgId == orgId
                && v.F员工ID == employeeId
                && v.F期间 == period
                && v.F处理状态 == 1)
            .ToListAsync();

        bool hasCrossZero = false;
        string? zeroReason = null;

        if (violations.Count > 0)
        {
            hasCrossZero = true;
            crossPositionValue = 0m;
            zeroReason = string.Join("；", violations.Select(v => GetViolationTypeDesc(v.F违规类型)));
        }

        // Step e: 计算综合质量等级
        int overallQualityLevel = CalcOverallQualityLevel(employeeRecords);

        // Step f: 计算 B 分变化
        int bScoreChange = overallQualityLevel switch
        {
            1 => +10,   // A 级
            2 => 0,     // B 级
            3 => -5,    // C 级
            4 => -20,   // D 级
            _ => 0,
        };

        // Step g: 计算 A 分变化
        int aScoreChange = 0;
        if (crossPositionValue > 0 && !hasCrossZero)
        {
            aScoreChange = (int)(crossPositionValue / 100m); // 向下取整
        }

        // Step h: 确定状态
        int status;
        if (hasCrossZero)
            status = 2; // 清零
        else
            status = 1; // 正常

        var totalValue = ownPositionValue + crossPositionValue;

        // Step 4: 幂等写入（事务）
        long savedResultId = 0;
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // DELETE 旧记录
            var oldResult = await _db.Set<PpvMonthlyResult>()
                .FirstOrDefaultAsync(r => r.FOrgId == orgId
                    && r.F员工ID == employeeId
                    && r.F期间 == period);
            if (oldResult != null)
            {
                _db.Remove(oldResult);
                await _db.SaveChangesAsync();
            }

            // INSERT 新记录
            var result = new PpvMonthlyResult
            {
                FOrgId = orgId,
                F员工ID = employeeId,
                F期间 = period,
                F总产值 = totalValue,
                F本岗产值 = ownPositionValue,
                F跨岗产值 = crossPositionValue,
                F综合质量等级 = overallQualityLevel,
                F是否跨岗清零 = hasCrossZero,
                F清零原因 = zeroReason,
                FB分变化 = bScoreChange,
                FA分变化 = aScoreChange,
                F岗位ID快照 = positionId,
                F部门ID快照 = deptId,
                F状态 = status,
                F创建时间 = DateTime.Now,
            };
            _db.Add(result);
            await _db.SaveChangesAsync();
            savedResultId = result.FID; // EF Core 在 SaveChangesAsync 后回填 IDENTITY 主键
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        // Step 5: 发布事件（仅 F状态=1 正常时发布，try-catch 保护主流程）
        if (status == 1)
        {
            try
            {
                await _eventDispatcher.PublishAsync(new PpvMonthlyAggregatedEvent
                {
                    OrgId = orgId,
                    EmployeeId = employeeId,
                    Period = period,
                    MonthlyResultId = savedResultId,
                    TotalProductValue = totalValue,
                    OverallQualityGrade = overallQualityLevel,
                    IsCrossPositionCleared = hasCrossZero,
                    BScoreChange = bScoreChange,
                    AScoreChange = aScoreChange
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发布 PpvMonthlyAggregatedEvent 失败：员工ID={EmployeeId}, 期间={Period}",
                    employeeId, period);
            }
        }
    }

    /// <summary>
    /// 计算综合质量等级。
    /// 若有任何 D 级 → 综合=4(D)
    /// 否则按占比：A级占比>=60% → 1(A), B级占比>=50% → 2(B), 其余 → 3(C)
    /// </summary>
    private static int CalcOverallQualityLevel(List<PpvRecord> records)
    {
        if (records.Count == 0) return 2; // 无记录默认 B

        // 若有任何 D 级（4）→ 综合=D
        if (records.Any(r => r.F质量等级 == 4))
            return 4;

        int total = records.Count;
        int aCount = records.Count(r => r.F质量等级 == 1);
        int bCount = records.Count(r => r.F质量等级 == 2);

        double aRatio = (double)aCount / total;
        double bRatio = (double)bCount / total;

        if (aRatio >= 0.6) return 1;  // A 级占比 >= 60%
        if (bRatio >= 0.5) return 2;  // B 级占比 >= 50%
        return 3; // 其余 → C
    }

    /// <summary>
    /// 违规类型描述映射。
    /// </summary>
    private static string GetViolationTypeDesc(int type) => type switch
    {
        1 => "质量违规",
        2 => "客诉",
        3 => "其他",
        _ => $"未知违规类型({type})",
    };

    /// <summary>
    /// period (yyyyMM) → 该月最后一刻 23:59:59.9999999。
    /// </summary>
    private static DateTime GetPeriodEndDate(string period)
    {
        if (period.Length != 6 || !int.TryParse(period[..4], out var year) || !int.TryParse(period[4..], out var month))
            throw new ArgumentException($"period 格式错误，应为 yyyyMM：{period}");
        var firstOfNext = new DateTime(year, month, 1).AddMonths(1);
        return firstOfNext.AddTicks(-1);
    }
}
