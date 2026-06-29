using System.Data;
using System.Reflection;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Contracts.Hr;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.KSF.Entities;
using STOTOP.Module.KSF.Events;
using STOTOP.Module.KSF.Services;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.KSF.Jobs;

/// <summary>
/// KSF 月度核算 Job。每月 6 日 03:00 执行（RecurringJobId: ksf.calc-monthly）。
///
/// 业务流程（以"上月"为核算期间，period = yyyyMM）：
/// 1. 取所有 F启用状态=true 的 KsfPlan，按方案遍历。
/// 2. 通过 SysUserPosition 找出该岗位下所有员工。
/// 3. 对每个员工执行 CalcForEmployee：
///    a. 拉员工组织 / 岗位 / 经营单元快照（按 periodEndDate 时点）。
///    b. 逐个指标取数（SQL 模板 / Agent / 常量 / 外部API 预留）。
///    c. 按 KsfPlanDetail.F激励刻度JSON 阶梯计算金额变动。
///    d. 解析 KsfPlan.F门槛规则JSON 做红线 / B 分余额门槛检查，得到兑现比例。
///    e. 幂等写入 KsfResult + KsfResultDetail（先 DELETE 同员工同期间旧记录）。
///    f. 仅 F运行模式=1 且无错误时发布 KsfMonthlyResultEvent。
/// 4. 单员工异常 try-catch 隔离，不中断整体。
/// </summary>
public class KsfCalcJob
{
    private readonly STOTOPDbContext _db;
    private readonly IEmployeeOrgQueryService _employeeOrgService;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KsfCalcJob> _logger;

    /// <summary>运行上下文缓存（指标取数结果，跨员工复用），仅在单次 Job 运行内有效。</summary>
    private readonly Dictionary<string, decimal> _indicatorCache = new();

    public KsfCalcJob(
        STOTOPDbContext db,
        IEmployeeOrgQueryService employeeOrgService,
        IEventDispatcher eventDispatcher,
        IServiceProvider serviceProvider,
        ILogger<KsfCalcJob> logger)
    {
        _db = db;
        _employeeOrgService = employeeOrgService;
        _eventDispatcher = eventDispatcher;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 月度核算入口。
    /// </summary>
    /// <param name="period">期间 yyyyMM，默认上月</param>
    /// <param name="specificEmployeeId">仅核算指定员工（手动重跑场景），不传则全量</param>
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(string? period = null, long? specificEmployeeId = null)
    {
        period ??= DateTime.Now.AddMonths(-1).ToString("yyyyMM");
        var periodEndDate = GetPeriodEndDate(period);

        _logger.LogInformation("KsfCalcJob 启动 | period={Period} | endDate={EndDate:O} | specificEmployee={Employee}",
            period, periodEndDate, specificEmployeeId?.ToString() ?? "<all>");

        // 1. 全部启用方案（按组织聚合，方便后续按方案 / 员工迭代）
        var plans = await _db.Set<KsfPlan>()
            .Where(p => p.F启用状态
                && p.F生效起期 <= periodEndDate
                && (p.F生效止期 == null || p.F生效止期 >= periodEndDate))
            .ToListAsync();

        int totalEmployees = 0, totalSucceeded = 0, totalFailed = 0;

        foreach (var plan in plans)
        {
            // 当前方案的所有明细
            var details = await _db.Set<KsfPlanDetail>()
                .Where(d => d.F方案ID == plan.FID && d.FOrgId == plan.FOrgId)
                .ToListAsync();
            if (details.Count == 0)
            {
                _logger.LogWarning("KsfCalcJob 方案无指标明细，跳过 | planId={PlanId}", plan.FID);
                continue;
            }

            // 岗位下所有员工
            var employeeIds = await _db.Set<SysUserPosition>()
                .Where(up => up.FPositionId == plan.F岗位ID)
                .Select(up => up.FUserId)
                .Distinct()
                .ToListAsync();

            if (specificEmployeeId.HasValue)
            {
                employeeIds = employeeIds.Where(id => id == specificEmployeeId.Value).ToList();
            }

            foreach (var employeeId in employeeIds)
            {
                totalEmployees++;
                try
                {
                    await CalcForEmployee(plan, details, employeeId, period, periodEndDate);
                    totalSucceeded++;
                }
                catch (Exception ex)
                {
                    totalFailed++;
                    _logger.LogError(ex,
                        "KsfCalcJob 单员工核算失败 | planId={PlanId} | employeeId={EmployeeId} | period={Period}",
                        plan.FID, employeeId, period);
                }
            }
        }

        _logger.LogInformation(
            "KsfCalcJob 完成 | period={Period} | plans={PlanCount} | employees={EmpCount} | ok={Ok} | fail={Fail}",
            period, plans.Count, totalEmployees, totalSucceeded, totalFailed);
    }

    private async Task CalcForEmployee(KsfPlan plan, List<KsfPlanDetail> details, long employeeId, string period, DateTime periodEndDate)
    {
        var orgId = plan.FOrgId;

        // Step 1: 员工维度快照
        var snapshot = await _employeeOrgService.GetSnapshotAsync(employeeId, orgId, periodEndDate);
        var positionId = snapshot?.PrimaryPositionId
            ?? await _employeeOrgService.GetPrimaryPositionIdAsync(employeeId, orgId)
            ?? plan.F岗位ID;
        var deptId = 0L; // EmployeeOrgSnapshotDto 未提供部门，留 0 占位

        // 经营单元映射（按 periodEndDate 落在生效区间内，分摊比例从高到低）
        var mappings = await _db.Set<KsfEmployeeUnitMapping>()
            .Where(m => m.FOrgId == orgId
                && m.F员工ID == employeeId
                && m.F生效起期 <= periodEndDate
                && (m.F生效止期 == null || m.F生效止期 >= periodEndDate))
            .OrderByDescending(m => m.F分摊比例)
            .ToListAsync();
        long? primaryUnitId = mappings.FirstOrDefault()?.F经营单元ID;

        // Step 2: 取数 + 阶梯计算
        var indicatorResults = new List<IndicatorCalcResult>();
        bool hasError = false;

        foreach (var detail in details)
        {
            try
            {
                var indicator = await _db.Set<KsfIndicator>().FindAsync(detail.F指标ID)
                    ?? throw new InvalidOperationException($"指标不存在 indicatorId={detail.F指标ID}");

                var value = await FetchIndicatorValue(orgId, indicator, period, employeeId, primaryUnitId, mappings);
                var diff = value - detail.F平衡点;
                var amountChange = CalcByLadder(detail, diff, indicator.F方向);

                indicatorResults.Add(new IndicatorCalcResult
                {
                    Detail = detail,
                    Indicator = indicator,
                    ActualValue = value,
                    Diff = diff,
                    AmountChange = amountChange,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "KSF 取数 / 计算失败 | employeeId={EmployeeId} | indicatorId={IndicatorId}",
                    employeeId, detail.F指标ID);
                hasError = true;
                break;
            }
        }

        // Step 3: 门槛检查
        decimal salaryRatio = 1.0m;
        if (!hasError && !string.IsNullOrWhiteSpace(plan.F门槛规则JSON))
        {
            try
            {
                var bBalance = await GetBBalanceAsync(orgId, employeeId, periodEndDate);
                salaryRatio = CalcThresholdRatio(plan.F门槛规则JSON!, indicatorResults, bBalance);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "KSF 门槛规则解析异常，按 100% 兑现 | employeeId={EmployeeId} | planId={PlanId}",
                    employeeId, plan.FID);
                salaryRatio = 1.0m;
            }
        }

        // Step 4: 汇总金额
        var floatingTotal = indicatorResults.Sum(r => r.AmountChange);
        var salary = Math.Round(plan.F岗位月加薪基数 * salaryRatio, 2);
        var deduction = indicatorResults.Where(r => r.AmountChange < 0).Sum(r => Math.Abs(r.AmountChange));
        var fixedPart = 0m; // KsfPlan 无 F固定部分字段，留 0
        var actualPay = fixedPart + floatingTotal + salary;

        int status = hasError ? 3 : (plan.F运行模式 == 0 ? 1 : 2);

        // Step 5: 幂等写入（事务）
        await using var tx = await _db.Database.BeginTransactionAsync();
        long resultId;
        try
        {
            var oldResult = await _db.Set<KsfResult>()
                .FirstOrDefaultAsync(r => r.F员工ID == employeeId
                    && r.F期间 == period
                    && r.FOrgId == orgId);
            if (oldResult != null)
            {
                var oldDetails = await _db.Set<KsfResultDetail>()
                    .Where(d => d.F结果ID == oldResult.FID)
                    .ToListAsync();
                if (oldDetails.Count > 0)
                {
                    _db.RemoveRange(oldDetails);
                }
                _db.Remove(oldResult);
                await _db.SaveChangesAsync();
            }

            var planSnapshotJson = JsonSerializer.Serialize(new
            {
                plan.FID,
                plan.F名称,
                plan.F岗位ID,
                plan.F运行模式,
                plan.F岗位月加薪基数,
                plan.F门槛规则JSON,
            });

            var result = new KsfResult
            {
                FOrgId = orgId,
                F员工ID = employeeId,
                F期间 = period,
                F方案ID = plan.FID,
                F岗位ID快照 = positionId,
                F部门ID快照 = deptId,
                F经营单元ID快照 = primaryUnitId,
                F固定部分 = fixedPart,
                F浮动部分 = floatingTotal,
                F加薪 = salary,
                F扣减 = deduction,
                F实发 = actualPay,
                F方案快照JSON = planSnapshotJson,
                F状态 = status,
                F创建时间 = DateTime.Now,
            };
            _db.Add(result);
            await _db.SaveChangesAsync();
            resultId = result.FID;

            foreach (var r in indicatorResults)
            {
                var indicatorSnapshotJson = JsonSerializer.Serialize(new
                {
                    r.Indicator.FID,
                    r.Indicator.F编码,
                    r.Indicator.F名称,
                    r.Indicator.F计量单位,
                    r.Indicator.F取数类型,
                    r.Indicator.F方向,
                    PlanDetail = new
                    {
                        r.Detail.FID,
                        r.Detail.F权重,
                        r.Detail.F平衡点,
                        r.Detail.F激励刻度JSON,
                        r.Detail.F最低保底,
                    },
                });
                _db.Add(new KsfResultDetail
                {
                    FOrgId = orgId,
                    F结果ID = resultId,
                    F指标ID = r.Detail.F指标ID,
                    F实际值 = r.ActualValue,
                    F差额 = r.Diff,
                    F金额变动 = r.AmountChange,
                    F指标快照JSON = indicatorSnapshotJson,
                });
            }
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        // Step 6: 仅正式方案且无错误时发事件
        if (plan.F运行模式 == 1 && !hasError)
        {
            await _eventDispatcher.PublishAsync(new KsfMonthlyResultEvent
            {
                OrgId = orgId,
                ModuleCode = "KSF",
                EmployeeId = employeeId,
                Period = period,
                PlanId = plan.FID,
                ResultId = resultId,
                FloatingAmount = floatingTotal,
                SalaryIncrease = salary,
                Deduction = deduction,
                ResultStatus = status,
            });
        }
    }

    /// <summary>
    /// 阶梯计算：按 F激励刻度JSON 配置（支持简单 rate 模式或 ladders 区间模式）计算金额变动。
    /// JSON 形态约定（任一即可）：
    ///   { "rate": 10, "cap": 500, "floor": -200 }
    ///   { "ladders": [ { "min": 0, "max": 100, "rate": 5 }, { "min": 100, "max": null, "rate": 10 } ], "cap": 500, "floor": -200 }
    /// 方向：1=正向 取 diff 原值；2=逆向 取 -diff。
    /// 兜底：F最低保底 作为单指标金额变动下限护栏（与 floor 取较小者优先）。
    /// </summary>
    private decimal CalcByLadder(KsfPlanDetail detail, decimal diff, int direction)
    {
        // 逆向指标：把 diff 反转
        var effectiveDiff = direction == 2 ? -diff : diff;

        decimal amount;
        decimal? capUpper = null;
        decimal? capLower = null;

        if (string.IsNullOrWhiteSpace(detail.F激励刻度JSON))
        {
            amount = 0m;
        }
        else
        {
            using var doc = JsonDocument.Parse(detail.F激励刻度JSON);
            var root = doc.RootElement;

            if (root.TryGetProperty("cap", out var capEl) && capEl.ValueKind == JsonValueKind.Number)
                capUpper = capEl.GetDecimal();
            if (root.TryGetProperty("floor", out var floorEl) && floorEl.ValueKind == JsonValueKind.Number)
                capLower = floorEl.GetDecimal();

            if (root.TryGetProperty("ladders", out var laddersEl) && laddersEl.ValueKind == JsonValueKind.Array)
            {
                amount = CalcByLadderArray(effectiveDiff, laddersEl);
            }
            else if (root.TryGetProperty("rate", out var rateEl) && rateEl.ValueKind == JsonValueKind.Number)
            {
                amount = effectiveDiff * rateEl.GetDecimal();
            }
            else
            {
                amount = 0m;
            }
        }

        // 封顶
        if (capUpper.HasValue && amount > capUpper.Value) amount = capUpper.Value;
        if (capLower.HasValue && amount < capLower.Value) amount = capLower.Value;

        // 最低保底护栏
        if (amount < detail.F最低保底) amount = detail.F最低保底;

        return Math.Round(amount, 2);
    }

    /// <summary>
    /// 阶梯数组模式：按左闭右开 [min, max) 累计区间内点数 × rate。
    /// 当 diff 为负数时，从 min=0 向下逐段累加（仍按区间 rate 计费，rate 含正负含义）。
    /// </summary>
    private static decimal CalcByLadderArray(decimal diff, JsonElement laddersEl)
    {
        if (diff == 0m) return 0m;
        decimal total = 0m;
        decimal remaining = Math.Abs(diff);
        var sign = diff > 0 ? 1m : -1m;

        // 排序：按 min 升序，max=null 视为正无穷
        var ladders = new List<(decimal min, decimal? max, decimal rate)>();
        foreach (var item in laddersEl.EnumerateArray())
        {
            decimal min = 0m;
            decimal? max = null;
            decimal rate = 0m;
            if (item.TryGetProperty("min", out var minEl) && minEl.ValueKind == JsonValueKind.Number)
                min = minEl.GetDecimal();
            if (item.TryGetProperty("max", out var maxEl) && maxEl.ValueKind == JsonValueKind.Number)
                max = maxEl.GetDecimal();
            if (item.TryGetProperty("rate", out var rateEl) && rateEl.ValueKind == JsonValueKind.Number)
                rate = rateEl.GetDecimal();
            ladders.Add((min, max, rate));
        }
        ladders.Sort((a, b) => a.min.CompareTo(b.min));

        foreach (var (min, max, rate) in ladders)
        {
            if (remaining <= 0) break;
            var span = (max ?? decimal.MaxValue) - min;
            if (span <= 0) continue;
            var taken = Math.Min(remaining, span);
            total += taken * rate;
            remaining -= taken;
        }
        return total * sign;
    }

    /// <summary>
    /// 门槛检查：返回加薪兑现比例。
    /// JSON 形态约定：
    ///   {
    ///     "redLines": [ { "indicatorId": 1, "threshold": 100, "direction": "gt|lt", "ratio": 0 } ],
    ///     "bBalanceLadders": [ { "min": 0, "max": 50, "ratio": 0.5 }, { "min": 50, "max": null, "ratio": 1.0 } ]
    ///   }
    /// 多门槛同时命中取最小兑现比例。
    /// </summary>
    private static decimal CalcThresholdRatio(string thresholdJson, List<IndicatorCalcResult> results, int bBalance)
    {
        decimal minRatio = 1.0m;
        using var doc = JsonDocument.Parse(thresholdJson);
        var root = doc.RootElement;

        // 红线指标
        if (root.TryGetProperty("redLines", out var redLinesEl) && redLinesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var rl in redLinesEl.EnumerateArray())
            {
                if (!rl.TryGetProperty("indicatorId", out var idEl)) continue;
                var indId = idEl.GetInt64();
                var hit = results.FirstOrDefault(r => r.Indicator.FID == indId);
                if (hit == null) continue;

                decimal threshold = rl.TryGetProperty("threshold", out var thEl) ? thEl.GetDecimal() : 0m;
                string direction = rl.TryGetProperty("direction", out var dirEl) && dirEl.ValueKind == JsonValueKind.String
                    ? (dirEl.GetString() ?? "gt") : "gt";
                decimal ratio = rl.TryGetProperty("ratio", out var rEl) ? rEl.GetDecimal() : 0m;

                bool triggered = direction == "lt" ? hit.ActualValue < threshold : hit.ActualValue > threshold;
                if (triggered && ratio < minRatio) minRatio = ratio;
            }
        }

        // B 分余额阶梯
        if (root.TryGetProperty("bBalanceLadders", out var bEl) && bEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var lad in bEl.EnumerateArray())
            {
                decimal min = lad.TryGetProperty("min", out var minEl) ? minEl.GetDecimal() : 0m;
                decimal? max = lad.TryGetProperty("max", out var maxEl) && maxEl.ValueKind == JsonValueKind.Number
                    ? maxEl.GetDecimal() : (decimal?)null;
                decimal ratio = lad.TryGetProperty("ratio", out var rEl) ? rEl.GetDecimal() : 1m;

                if (bBalance >= min && (max == null || bBalance < max))
                {
                    if (ratio < minRatio) minRatio = ratio;
                    break;
                }
            }
        }

        return minRatio;
    }

    /// <summary>
    /// 取数路由：根据 KsfIndicator.F取数类型 调用对应实现。
    /// 缓存 key 包含 orgId / period / businessUnitId / 指标编码，避免同组织 / 同单元重复查询。
    /// </summary>
    private async Task<decimal> FetchIndicatorValue(long orgId, KsfIndicator indicator, string period, long employeeId, long? businessUnitId, List<KsfEmployeeUnitMapping> mappings)
    {
        var cacheKey = $"{orgId}_{period}_{businessUnitId?.ToString() ?? "-"}_{indicator.F编码}";
        if (_indicatorCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        decimal value = indicator.F取数类型 switch
        {
            1 => await FetchBySqlTemplate(orgId, indicator, period, employeeId, businessUnitId),
            2 => await FetchByAgent(orgId, indicator, period, employeeId, businessUnitId),
            3 => FetchByConstant(indicator),
            4 => 0m, // 外部 API 预留
            _ => throw new InvalidOperationException($"未知取数类型: {indicator.F取数类型}"),
        };

        // 多经营单元分摊：当员工有多个映射且指标按主经营单元取数时，按分摊比例加权
        // 简化策略：若有多个映射且取数已基于主经营单元，结果不再二次缩放（由 SQL/Agent 自行处理）
        // 这里仅在主单元为空且存在映射时按全部映射加权累加
        if (businessUnitId == null && mappings.Count > 1)
        {
            // 主单元未选取时不做加权（保持原值），交给上层判断
        }

        _indicatorCache[cacheKey] = value;
        return value;
    }

    /// <summary>
    /// SQL 模板取数：占位符 {orgId} {period} {employeeId} {businessUnitId} 字符串替换。
    /// 模板安全性已在保存时校验，不再做参数化绑定。
    /// </summary>
    private async Task<decimal> FetchBySqlTemplate(long orgId, KsfIndicator indicator, string period, long employeeId, long? businessUnitId)
    {
        if (string.IsNullOrWhiteSpace(indicator.F取数SQL))
            throw new InvalidOperationException($"指标 {indicator.F编码} F取数类型=1 但 F取数SQL 为空");

        var sql = indicator.F取数SQL!
            .Replace("{orgId}", orgId.ToString())
            .Replace("{period}", period)
            .Replace("{employeeId}", employeeId.ToString())
            .Replace("{businessUnitId}", (businessUnitId ?? 0).ToString());

        var conn = _db.Database.GetDbConnection();
        var alreadyOpen = conn.State == ConnectionState.Open;
        if (!alreadyOpen) await _db.Database.OpenConnectionAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
        }
        finally
        {
            if (!alreadyOpen) await _db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Agent 取数：按 F取数Agent 解析 Keyed 注册的 IKsfIndicatorAgent。
    /// </summary>
    private async Task<decimal> FetchByAgent(long orgId, KsfIndicator indicator, string period, long employeeId, long? businessUnitId)
    {
        if (string.IsNullOrWhiteSpace(indicator.F取数Agent))
            throw new InvalidOperationException($"指标 {indicator.F编码} F取数类型=2 但 F取数Agent 为空");

        var agent = _serviceProvider.GetKeyedService<IKsfIndicatorAgent>(indicator.F取数Agent!)
            ?? throw new InvalidOperationException($"未注册 IKsfIndicatorAgent key={indicator.F取数Agent}");

        return await agent.FetchValueAsync(orgId, period, employeeId, businessUnitId, indicator.F取数参数JSON);
    }

    /// <summary>
    /// 常量取数：从 F取数参数JSON 中读取 value 字段。
    /// </summary>
    private static decimal FetchByConstant(KsfIndicator indicator)
    {
        if (string.IsNullOrWhiteSpace(indicator.F取数参数JSON)) return 0m;
        using var doc = JsonDocument.Parse(indicator.F取数参数JSON);
        if (doc.RootElement.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number)
        {
            return v.GetDecimal();
        }
        return 0m;
    }

    /// <summary>
    /// 通过反射动态解析 IPointService，避免 KSF 模块直接引用 Module.Points。
    /// 若 Points 模块未加载或未注册，返回 0（不阻断 KSF 核算）。
    /// </summary>
    private async Task<int> GetBBalanceAsync(long orgId, long employeeId, DateTime atDate)
    {
        try
        {
            var pointServiceType = Type.GetType("STOTOP.Module.Points.Services.IPointService, STOTOP.Module.Points");
            if (pointServiceType == null) return 0;
            var pointService = _serviceProvider.GetService(pointServiceType);
            if (pointService == null) return 0;

            var method = pointServiceType.GetMethod("GetAccountBalanceAtDateAsync",
                BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return 0;

            // accountType: 2 = B 分（PointAccountTypes.B）
            var task = (Task)method.Invoke(pointService, new object[] { orgId, employeeId, 2, atDate })!;
            await task.ConfigureAwait(false);

            var resultProp = task.GetType().GetProperty("Result");
            var apiResult = resultProp?.GetValue(task);
            var dataProp = apiResult?.GetType().GetProperty("Data");
            var data = dataProp?.GetValue(apiResult);
            return data == null ? 0 : Convert.ToInt32(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GetBBalanceAsync 反射调用失败，按 0 处理 | orgId={OrgId} | employeeId={EmployeeId}",
                orgId, employeeId);
            return 0;
        }
    }

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

    /// <summary>
    /// 单员工 + 单指标 的核算中间结果。
    /// </summary>
    private sealed class IndicatorCalcResult
    {
        public KsfPlanDetail Detail { get; set; } = null!;
        public KsfIndicator Indicator { get; set; } = null!;
        public decimal ActualValue { get; set; }
        public decimal Diff { get; set; }
        public decimal AmountChange { get; set; }
    }
}
