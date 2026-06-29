using System.Reflection;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Salary.Entities;
using STOTOP.Module.Salary.Services;

namespace STOTOP.Module.Salary.Jobs;

/// <summary>
/// B 分月度清零兑换 Job。每月 6 日 04:30 执行（RecurringJobId: sal.bscore-monthly-reset）。
///
/// 时序：
///   每月6日 03:00  KSF核算(上月) → 产生B分变动
///   每月6日 04:00  PPV汇总(上月) → 产生B分变动
///   每月6日 04:30  B分月清 → 清零本轮累积B分, 写兑换记录
///   每月6日 05:00  工资结算 → 读取B分兑换金额纳入工资单
///
/// 逻辑：查 B 分余额 > 0 → 计算兑换金额 → 反射调用 DeductAsync 清零 → 写 SalaryBScoreConversion 记录
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 3600)]
[AutomaticRetry(Attempts = 0)]
public class BScoreResetJob
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<BScoreResetJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ISalaryConfigService _configService;

    public BScoreResetJob(
        STOTOPDbContext db,
        ILogger<BScoreResetJob> logger,
        IServiceProvider serviceProvider,
        IEventDispatcher eventDispatcher,
        ISalaryConfigService configService)
    {
        _db = db;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _eventDispatcher = eventDispatcher;
        _configService = configService;
    }

    /// <summary>
    /// B 分月清入口。
    /// </summary>
    /// <param name="period">期间 yyyyMM，默认上月</param>
    /// <param name="specificEmployeeId">仅处理指定员工（手动重跑场景），不传则全量</param>
    public async Task Execute(string? period = null, long? specificEmployeeId = null)
    {
        period ??= DateTime.Now.AddMonths(-1).ToString("yyyyMM");

        _logger.LogInformation("BScoreResetJob 启动 | period={Period} | specificEmployee={Employee}",
            period, specificEmployeeId?.ToString() ?? "<all>");

        // 1. 查询所有 B 分余额 > 0 的账户（F账户类型=2 即 B 分）
        List<BScoreAccountInfo> bScoreAccounts;
        if (specificEmployeeId.HasValue)
        {
            bScoreAccounts = await _db.Database.SqlQuery<BScoreAccountInfo>(
                $"SELECT F用户ID AS UserId, F组织ID AS OrgId, F当前积分 AS Balance FROM PM积分账户 WHERE F账户类型 = 2 AND F当前积分 > 0 AND F用户ID = {specificEmployeeId.Value}"
            ).ToListAsync();
        }
        else
        {
            bScoreAccounts = await _db.Database.SqlQuery<BScoreAccountInfo>(
                $"SELECT F用户ID AS UserId, F组织ID AS OrgId, F当前积分 AS Balance FROM PM积分账户 WHERE F账户类型 = 2 AND F当前积分 > 0"
            ).ToListAsync();
        }

        if (bScoreAccounts.Count == 0)
        {
            _logger.LogInformation("BScoreResetJob 无 B 分余额 > 0 的账户，跳过 | period={Period}", period);
            return;
        }

        int totalProcessed = 0, totalFailed = 0;
        decimal totalConvertedAmount = 0m;

        // 2. 逐员工处理（try-catch 隔离）
        foreach (var account in bScoreAccounts)
        {
            try
            {
                // a. 计算兑换金额
                decimal conversionAmount = account.Balance * _configService.GetBScoreToYuanRate(account.OrgId);

                // b. 通过反射动态解析 IPointService 执行扣减（Salary 不引用 Module.Points）
                await DeductBScoreViaReflectionAsync(account, period);

                // c. 幂等写入 SalaryBScoreConversion 记录（先删旧记录再插入）
                var existing = await _db.Set<SalaryBScoreConversion>()
                    .Where(c => c.F员工ID == account.UserId && c.F期间 == period && c.FOrgId == account.OrgId)
                    .FirstOrDefaultAsync();
                if (existing != null)
                    _db.Set<SalaryBScoreConversion>().Remove(existing);

                _db.Set<SalaryBScoreConversion>().Add(new SalaryBScoreConversion
                {
                    FOrgId = account.OrgId,
                    F员工ID = account.UserId,
                    F期间 = period,
                    FB分余额 = account.Balance,
                    F兑换比例 = _configService.GetBScoreToYuanRate(account.OrgId),
                    F兑换金额 = conversionAmount,
                    F兑换类型 = 2 // 2=工资
                });
                await _db.SaveChangesAsync();

                // d. 发布 PointResetEvent 供下游模块（KSF/排行榜等）联动消费
                await _eventDispatcher.PublishAsync(new PointResetEvent
                {
                    UserId = account.UserId,
                    AccountType = 2, // B分
                    ResetType = 1,   // 1=月清
                    BalanceBeforeReset = account.Balance,
                    BalanceAfterReset = 0,
                    ConvertedToVoucherValue = conversionAmount,
                    PeriodKey = period
                });

                totalProcessed++;
                totalConvertedAmount += conversionAmount;
            }
            catch (Exception ex)
            {
                totalFailed++;
                _logger.LogError(ex,
                    "BScoreResetJob 单员工处理失败 | userId={UserId} | orgId={OrgId} | period={Period}",
                    account.UserId, account.OrgId, period);
            }
        }

        _logger.LogInformation(
            "BScoreResetJob 完成 | period={Period} | total={Total} | ok={Ok} | fail={Fail} | convertedAmount={Amount:F2}",
            period, bScoreAccounts.Count, totalProcessed, totalFailed, totalConvertedAmount);
    }

    /// <summary>
    /// 通过反射动态解析 IPointService 并调用 DeductAsync 清零 B 分。
    /// 避免 Salary 模块直接引用 Module.Points 产生循环依赖。
    /// </summary>
    private async Task DeductBScoreViaReflectionAsync(BScoreAccountInfo account, string period)
    {
        var pointServiceType = Type.GetType("STOTOP.Module.Points.Services.IPointService, STOTOP.Module.Points");
        if (pointServiceType == null)
            throw new InvalidOperationException("无法加载 IPointService 类型，Module.Points 可能未部署");

        using var scope = _serviceProvider.CreateScope();
        var pointService = scope.ServiceProvider.GetService(pointServiceType);
        if (pointService == null)
            throw new InvalidOperationException("IPointService 未在 DI 容器中注册");

        // 构造 ManualDeductRequest 实例（通过反射）
        var requestType = Type.GetType("STOTOP.Module.Points.Dtos.ManualDeductRequest, STOTOP.Module.Points");
        if (requestType == null)
            throw new InvalidOperationException("无法加载 ManualDeductRequest 类型");

        var request = Activator.CreateInstance(requestType)!;
        requestType.GetProperty("UserId")!.SetValue(request, account.UserId);
        requestType.GetProperty("PointValue")!.SetValue(request, account.Balance);
        requestType.GetProperty("Remark")!.SetValue(request, $"B分月清兑换({period})");
        requestType.GetProperty("RelatedEventType")!.SetValue(request, "BScoreMonthlyReset");
        requestType.GetProperty("RelatedEventId")!.SetValue(request, $"{account.UserId}_{period}");
        requestType.GetProperty("RelatedModule")!.SetValue(request, "Salary");

        // DeductAsync(long orgId, long operatorId, ManualDeductRequest request, int accountType = 2)
        var deductMethod = pointServiceType.GetMethod("DeductAsync",
            BindingFlags.Public | BindingFlags.Instance);
        if (deductMethod == null)
            throw new InvalidOperationException("IPointService.DeductAsync 方法未找到");

        // operatorId 使用 0（系统自动操作），accountType = 2（B分）
        var task = (Task)deductMethod.Invoke(pointService, new object[] { account.OrgId, 0L, request, 2 })!;
        await task.ConfigureAwait(false);

        // 检查返回结果（ApiResult.IsSuccess）
        var resultProp = task.GetType().GetProperty("Result");
        var apiResult = resultProp?.GetValue(task);
        if (apiResult != null)
        {
            var isSuccessProp = apiResult.GetType().GetProperty("IsSuccess");
            var isSuccess = isSuccessProp?.GetValue(apiResult) as bool?;
            if (isSuccess == false)
            {
                var messageProp = apiResult.GetType().GetProperty("Message");
                var message = messageProp?.GetValue(apiResult) as string;
                _logger.LogWarning(
                    "BScoreResetJob DeductAsync 返回失败 | userId={UserId} | message={Message}",
                    account.UserId, message ?? "unknown");
            }
        }
    }

    /// <summary>SQL 查询结果映射记录</summary>
    private record BScoreAccountInfo(long UserId, long OrgId, int Balance);
}
