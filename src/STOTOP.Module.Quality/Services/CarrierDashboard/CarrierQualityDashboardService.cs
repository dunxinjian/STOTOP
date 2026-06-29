using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Quality.Dtos.CarrierDashboard;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Services.CarrierDashboard;

public class CarrierQualityDashboardService : ICarrierQualityDashboardService
{
    private readonly STOTOPDbContext _db;

    public CarrierQualityDashboardService(STOTOPDbContext db)
    {
        _db = db;
    }

    // ───────────────────────── 纯函数口径（可单测）─────────────────────────

    /// <summary>件量加权平均：Σ(value*weight)/Σweight；权重和为0或全空时退算术平均；无有效值返回 null。</summary>
    public static decimal? WeightedAverage(IEnumerable<(decimal? Value, decimal? Weight)> items)
    {
        decimal sumVW = 0m, sumW = 0m, sumV = 0m;
        int n = 0;
        foreach (var (v, w) in items)
        {
            if (v is null) continue;
            n++;
            sumV += v.Value;
            var weight = w ?? 0m;
            sumVW += v.Value * weight;
            sumW += weight;
        }
        if (n == 0) return null;
        return sumW > 0m ? sumVW / sumW : sumV / n;
    }

    /// <summary>期间累计：忽略 null 求和。</summary>
    public static decimal PeriodSum(IEnumerable<decimal?> values)
    {
        decimal s = 0m;
        foreach (var v in values) if (v.HasValue) s += v.Value;
        return s;
    }

    /// <summary>派送超时合计：T0..T3 求和，null 当 0。</summary>
    public static int TimeoutTotal(int? t0, int? t1, int? t2, int? t3)
        => (t0 ?? 0) + (t1 ?? 0) + (t2 ?? 0) + (t3 ?? 0);

    // ───────────────────────── 视图方法（Phase 1-3 实现）─────────────────────────
    // 占位：先抛 NotImplementedException，使接口可编译、DI 可注册；逐 Phase 替换为真实现。

    public async Task<ApiResult<NetworkKpiDto>> GetNetworkKpiAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode)
    {
        var toEnd = to.Date.AddDays(1); // 半开右界，含 to 当天
        var q = _db.Set<QlShentongNetworkDailyMetric>()
            .Where(m => m.FOrgId == orgId && m.F承运商 == carrier
                        && m.F业务日期 >= from.Date && m.F业务日期 < toEnd);
        if (!string.IsNullOrEmpty(networkCode))
            q = q.Where(m => m.F网点编码 == networkCode);

        var rows = await q.ToListAsync(); // 跨期合成在内存做（避免 EF 翻译末日/加权）
        var dto = new NetworkKpiDto();
        if (rows.Count > 0)
        {
            var lastDay = rows.Max(r => r.F业务日期).Date;
            var lastRows = rows.Where(r => r.F业务日期.Date == lastDay).ToList();
            // 率值=末日快照，多网点件量加权（权重 F日均出港量）
            decimal? Snap(Func<QlShentongNetworkDailyMetric, decimal?> sel)
                => WeightedAverage(lastRows.Select(r => (sel(r), (decimal?)r.F日均出港量)));

            dto.SignRateToday = Snap(r => r.F当天及时签收率);
            dto.SignRate48h = Snap(r => r.F48h签收率);
            dto.OutboundOnTimeRate = Snap(r => r.F一频次出仓及时率);
            dto.RetentionRate = Snap(r => r.F滞留率);
            dto.BacklogMultiple = Snap(r => r.F积压倍数);
            dto.LossRatePpm = Snap(r => r.F遗失率ppm);
            dto.FakeSignRate = Snap(r => r.F虚签投诉率);
            // 金额=期间累计
            dto.TotalAssessFee = PeriodSum(rows.Select(r => r.F考核金额合计));
        }

        // 问题件数=期间事件计数
        var evq = _db.Set<QlShentongQualityEvent>()
            .Where(e => e.FOrgId == orgId && e.F承运商 == carrier
                        && e.F业务日期 >= from.Date && e.F业务日期 < toEnd);
        if (!string.IsNullOrEmpty(networkCode))
            evq = evq.Where(e => e.F网点编码 == networkCode);
        dto.ProblemEventCount = await evq.CountAsync();

        return ApiResult<NetworkKpiDto>.Success(dto);
    }

    public async Task<ApiResult<List<NetworkTrendPointDto>>> GetNetworkTrendAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode)
    {
        var toEnd = to.Date.AddDays(1);
        var q = _db.Set<QlShentongNetworkDailyMetric>()
            .Where(m => m.FOrgId == orgId && m.F承运商 == carrier
                        && m.F业务日期 >= from.Date && m.F业务日期 < toEnd);
        if (!string.IsNullOrEmpty(networkCode))
            q = q.Where(m => m.F网点编码 == networkCode);

        var rows = await q.ToListAsync();
        var result = rows
            .GroupBy(r => r.F业务日期.Date)
            .OrderBy(g => g.Key)
            .Select(g => new NetworkTrendPointDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                SignRateToday = WeightedAverage(g.Select(r => (r.F当天及时签收率, (decimal?)r.F日均出港量))),
                OutboundOnTimeRate = WeightedAverage(g.Select(r => (r.F一频次出仓及时率, (decimal?)r.F日均出港量))),
                RetentionRate = WeightedAverage(g.Select(r => (r.F滞留率, (decimal?)r.F日均出港量))),
                FakeSignRate = WeightedAverage(g.Select(r => (r.F虚签投诉率, (decimal?)r.F日均出港量))),
            })
            .ToList();

        return ApiResult<List<NetworkTrendPointDto>>.Success(result);
    }
    public async Task<ApiResult<List<DomainStatItem>>> GetDomainDistributionAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode)
    {
        var toEnd = to.Date.AddDays(1);
        var q = _db.Set<QlShentongQualityEvent>()
            .Where(e => e.FOrgId == orgId && e.F承运商 == carrier
                        && e.F业务日期 >= from.Date && e.F业务日期 < toEnd);
        if (!string.IsNullOrEmpty(networkCode))
            q = q.Where(e => e.F网点编码 == networkCode);

        var data = await q
            .GroupBy(e => e.F质量域)
            .Select(g => new { Domain = g.Key, Count = g.Count() })
            .ToListAsync();

        var result = data
            .OrderByDescending(d => d.Count)
            .Select(d => new DomainStatItem { Domain = d.Domain, Count = d.Count })
            .ToList();

        return ApiResult<List<DomainStatItem>>.Success(result);
    }

    public async Task<ApiResult<List<DomainStatItem>>> GetFeeByDomainAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode)
    {
        var toEnd = to.Date.AddDays(1);
        var q = _db.Set<QlShentongNetworkDailyMetric>()
            .Where(m => m.FOrgId == orgId && m.F承运商 == carrier
                        && m.F业务日期 >= from.Date && m.F业务日期 < toEnd);
        if (!string.IsNullOrEmpty(networkCode))
            q = q.Where(m => m.F网点编码 == networkCode);

        var rows = await q.ToListAsync();
        var result = new List<DomainStatItem>
        {
            new() { Domain = "出仓", Fee = PeriodSum(rows.Select(r => r.F出仓预估考核金额)) },
            new() { Domain = "滞留", Fee = PeriodSum(rows.Select(r => r.F滞留预估考核金额)) },
            new() { Domain = "派送", Fee = PeriodSum(rows.Select(r => r.F派送预估考核金额)) },
            new() { Domain = "签收", Fee = PeriodSum(rows.Select(r => r.F签收率考核金额)) },
        };
        result = result.Where(x => x.Fee != 0m).OrderByDescending(x => x.Fee).ToList();

        return ApiResult<List<DomainStatItem>>.Success(result);
    }

    public async Task<ApiResult<List<NetworkOptionDto>>> GetNetworkOptionsAsync(long orgId, string carrier)
    {
        // 候选网点 = 网点日指标 ∪ 事件(已绑编码)，按编码去重，取首个非空名称
        var fromMetric = await _db.Set<QlShentongNetworkDailyMetric>()
            .Where(m => m.FOrgId == orgId && m.F承运商 == carrier && m.F网点编码 != "")
            .Select(m => new { Code = m.F网点编码, Name = m.F网点名称 })
            .Distinct().ToListAsync();
        var fromEvent = await _db.Set<QlShentongQualityEvent>()
            .Where(e => e.FOrgId == orgId && e.F承运商 == carrier && e.F网点编码 != null && e.F网点编码 != "")
            .Select(e => new { Code = e.F网点编码!, Name = e.F网点名称 })
            .Distinct().ToListAsync();
        var result = fromMetric.Concat(fromEvent)
            .GroupBy(x => x.Code)
            .Select(g => new NetworkOptionDto { Code = g.Key, Name = g.Select(x => x.Name).FirstOrDefault(n => !string.IsNullOrEmpty(n)) })
            .OrderBy(x => x.Code)
            .ToList();
        return ApiResult<List<NetworkOptionDto>>.Success(result);
    }

    public async Task<ApiResult<EmployeeRankDto>> GetEmployeeRankAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode, string dimension, int topN)
    {
        var toEnd = to.Date.AddDays(1);
        if (topN <= 0) topN = 10;

        List<EmployeeRankItemDto> items;

        if (dimension == "problem")
        {
            // 问题件 = 质量事件计数（按工号聚合）
            var evq = _db.Set<QlShentongQualityEvent>()
                .Where(e => e.FOrgId == orgId && e.F承运商 == carrier
                            && e.F业务日期 >= from.Date && e.F业务日期 < toEnd
                            && e.F员工工号 != null && e.F员工工号 != "");
            if (!string.IsNullOrEmpty(networkCode))
                evq = evq.Where(e => e.F网点编码 == networkCode);

            var grouped = await evq
                .GroupBy(e => new { e.F员工工号, e.F员工姓名原文, e.F网点编码 })
                .Select(g => new { g.Key.F员工工号, g.Key.F员工姓名原文, g.Key.F网点编码, Count = g.Count() })
                .ToListAsync();

            items = grouped.Select(g => new EmployeeRankItemDto
            {
                EmpNo = g.F员工工号!,
                EmpName = g.F员工姓名原文,
                NetworkCode = g.F网点编码,
                Value = g.Count
            }).ToList();
        }
        else
        {
            // 客诉/虚签/超时/考核金额 = 员工日指标按工号期间汇总
            var mq = _db.Set<QlShentongEmployeeDailyMetric>()
                .Where(m => m.FOrgId == orgId && m.F承运商 == carrier
                            && m.F业务日期 >= from.Date && m.F业务日期 < toEnd
                            && m.F员工工号 != "");
            if (!string.IsNullOrEmpty(networkCode))
                mq = mq.Where(m => m.F网点编码 == networkCode);

            var rows = await mq.ToListAsync();
            items = rows
                .GroupBy(m => new { m.F员工工号, m.F员工姓名原文, m.F网点编码 })
                .Select(g => new EmployeeRankItemDto
                {
                    EmpNo = g.Key.F员工工号,
                    EmpName = g.Key.F员工姓名原文,
                    NetworkCode = g.Key.F网点编码,
                    Value = dimension switch
                    {
                        "complaint" => g.Sum(m => m.F客诉发起量 ?? 0),
                        "fakesign" => g.Sum(m => m.F虚假签收数 ?? 0),
                        "timeout" => g.Sum(m => TimeoutTotal(m.F派送超时T0数, m.F派送超时T1数, m.F派送超时T2数, m.F派送超时T3数)),
                        "fee" => g.Sum(m => m.F考核金额合计 ?? 0m),
                        _ => 0m
                    }
                })
                .ToList();
        }

        // 同一工号跨网点可能出现多行（数据噪声/调动），按工号合并：值求和、网点取值最高的子行，避免榜单重复工号
        items = items
            .GroupBy(x => x.EmpNo)
            .Select(g => new EmployeeRankItemDto
            {
                EmpNo = g.Key,
                EmpName = g.Select(x => x.EmpName).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
                NetworkCode = g.OrderByDescending(x => x.Value).First().NetworkCode,
                Value = g.Sum(x => x.Value)
            })
            .ToList();

        var dto = new EmployeeRankDto
        {
            Dimension = dimension,
            Worst = items.OrderByDescending(x => x.Value).Take(topN).ToList(),
            Best = items.Where(x => x.Value > 0).OrderBy(x => x.Value).Take(topN).ToList()
        };
        return ApiResult<EmployeeRankDto>.Success(dto);
    }
    public async Task<ApiResult<EmployeeMetricsPageDto>> GetEmployeeMetricsAsync(long orgId, string carrier, DateTime from, DateTime to, string? networkCode, int page, int size)
    {
        var toEnd = to.Date.AddDays(1);
        if (page <= 0) page = 1;
        if (size <= 0) size = 50;

        var mq = _db.Set<QlShentongEmployeeDailyMetric>()
            .Where(m => m.FOrgId == orgId && m.F承运商 == carrier
                        && m.F业务日期 >= from.Date && m.F业务日期 < toEnd
                        && m.F员工工号 != "");
        if (!string.IsNullOrEmpty(networkCode))
            mq = mq.Where(m => m.F网点编码 == networkCode);

        var rows = await mq.ToListAsync(); // 按工号期间 SUM 在内存聚合
        var grouped = rows
            .GroupBy(m => m.F员工工号) // 按工号聚合（工号×期），保证工号唯一（前端 row-key=empNo）；跨网点汇总，网点取首个
            .Select(g => new EmployeeMetricRowDto
            {
                EmpNo = g.Key,
                EmpName = g.Select(m => m.F员工姓名原文).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
                NetworkCode = g.Select(m => m.F网点编码).FirstOrDefault(),
                派件量 = g.Sum(m => m.F派件量 ?? 0),
                当日派签量 = g.Sum(m => m.F当日派签量 ?? 0),
                应上门量 = g.Sum(m => m.F应上门量 ?? 0),
                未上门量 = g.Sum(m => m.F未上门量 ?? 0),
                客诉发起量 = g.Sum(m => m.F客诉发起量 ?? 0),
                工单定责量 = g.Sum(m => m.F工单定责量 ?? 0),
                虚假签收数 = g.Sum(m => m.F虚假签收数 ?? 0),
                照片质检不合格数 = g.Sum(m => m.F照片质检不合格数 ?? 0),
                派送超时T0数 = g.Sum(m => m.F派送超时T0数 ?? 0),
                派送超时T1数 = g.Sum(m => m.F派送超时T1数 ?? 0),
                派送超时T2数 = g.Sum(m => m.F派送超时T2数 ?? 0),
                派送超时T3数 = g.Sum(m => m.F派送超时T3数 ?? 0),
                揽收不及时数 = g.Sum(m => m.F揽收不及时数 ?? 0),
                上传不及时数 = g.Sum(m => m.F上传不及时数 ?? 0),
                问题件数 = g.Sum(m => m.F问题件数 ?? 0),
                违规虚假电联 = g.Sum(m => m.F违规虚假电联 ?? 0),
                违规无效电联 = g.Sum(m => m.F违规无效电联 ?? 0),
                违规双签 = g.Sum(m => m.F违规双签 ?? 0),
                违规照片定位虚假 = g.Sum(m => m.F违规照片定位虚假 ?? 0),
                违规签收文本不规范 = g.Sum(m => m.F违规签收文本不规范 ?? 0),
                违规引导代收 = g.Sum(m => m.F违规引导代收 ?? 0),
                考核金额合计 = g.Sum(m => m.F考核金额合计 ?? 0m),
            })
            .OrderByDescending(x => x.考核金额合计)
            .ToList();

        var dto = new EmployeeMetricsPageDto
        {
            Total = grouped.Count,
            Items = grouped.Skip((page - 1) * size).Take(size).ToList()
        };
        return ApiResult<EmployeeMetricsPageDto>.Success(dto);
    }
    public async Task<ApiResult<List<EmployeeEventItemDto>>> GetEmployeeTimelineAsync(long orgId, string carrier, string empNo, DateTime from, DateTime to)
    {
        var toEnd = to.Date.AddDays(1);
        var items = await _db.Set<QlShentongQualityEvent>()
            .Where(e => e.FOrgId == orgId && e.F承运商 == carrier
                        && e.F员工工号 == empNo
                        && e.F业务日期 >= from.Date && e.F业务日期 < toEnd)
            .OrderByDescending(e => e.F业务日期)
            .Select(e => new EmployeeEventItemDto
            {
                Date = e.F业务日期,
                Waybill = e.F运单号,
                NetworkName = e.F网点名称,
                Domain = e.F质量域,
                ProblemName = e.F问题类型名称,
                Severity = e.F严重度,
                Fee = e.F考核金额,
            })
            .ToListAsync();

        return ApiResult<List<EmployeeEventItemDto>>.Success(items);
    }
    public async Task<ApiResult<EventPageDto>> GetEventsAsync(long orgId, EventQuery query)
    {
        var toEnd = query.To.Date.AddDays(1);
        var page = query.Page <= 0 ? 1 : query.Page;
        var size = query.Size <= 0 ? 50 : query.Size;

        var baseQ = _db.Set<QlShentongQualityEvent>()
            .Where(e => e.FOrgId == orgId && e.F承运商 == query.Carrier
                        && e.F业务日期 >= query.From.Date && e.F业务日期 < toEnd);
        if (!string.IsNullOrEmpty(query.NetworkCode)) baseQ = baseQ.Where(e => e.F网点编码 == query.NetworkCode);
        if (!string.IsNullOrEmpty(query.EmpNo)) baseQ = baseQ.Where(e => e.F员工工号 == query.EmpNo);
        if (!string.IsNullOrEmpty(query.Domain)) baseQ = baseQ.Where(e => e.F质量域 == query.Domain);
        if (!string.IsNullOrEmpty(query.Platform)) baseQ = baseQ.Where(e => e.F电商平台 == query.Platform);
        if (query.Severity.HasValue) baseQ = baseQ.Where(e => e.F严重度 == query.Severity.Value);

        // 多域命中：同一运单 ≥2 个 distinct 质量域。先拉 (运单,域) 去重对再内存分组（避开 EF 对 Distinct().Count() 的翻译坑）
        var pairs = await baseQ.Where(e => e.F运单号 != null && e.F运单号 != "")
            .Select(e => new { e.F运单号, e.F质量域 })
            .Distinct()
            .ToListAsync();
        var multiWaybills = pairs
            .GroupBy(p => p.F运单号!)
            .Where(g => g.Select(x => x.F质量域).Distinct().Count() >= 2)
            .Select(g => g.Key)
            .ToHashSet();

        // 注：MultiDomainOnly 经内存集合 Contains→翻译为 SQL IN。一期单组织申通数据量有限（多域命中运单数远低于 SQL Server 2100 参数上限）。
        // 二期多承运商/多网点放量后，若单期多域命中运单 >2100，需改为 EXISTS 子查询或内存分页（见 design spec 二期边界）。
        if (query.MultiDomainOnly)
            baseQ = baseQ.Where(e => e.F运单号 != null && multiWaybills.Contains(e.F运单号));
        if (query.PendingOnly)
            baseQ = baseQ.Where(e => e.F员工匹配状态 == 0 || e.F员工匹配状态 == 3);

        var total = await baseQ.CountAsync();
        var pageRows = await baseQ
            .OrderByDescending(e => e.F业务日期).ThenByDescending(e => e.FID)
            .Skip((page - 1) * size).Take(size)
            .Select(e => new QualityEventRowDto
            {
                Id = e.FID,
                Date = e.F业务日期,
                Waybill = e.F运单号,
                NetworkCode = e.F网点编码,
                NetworkName = e.F网点名称,
                EmpNo = e.F员工工号,
                EmpNameRaw = e.F员工姓名原文,
                Domain = e.F质量域,
                ProblemName = e.F问题类型名称,
                Severity = e.F严重度,
                IsAssessed = e.F是否考核件,
                Fee = e.F考核金额,
                Platform = e.F电商平台,
                IsPending = e.F员工匹配状态 == 0 || e.F员工匹配状态 == 3,
            })
            .ToListAsync();

        foreach (var r in pageRows)
            r.IsMultiDomain = r.Waybill != null && multiWaybills.Contains(r.Waybill);

        return ApiResult<EventPageDto>.Success(new EventPageDto { Items = pageRows, Total = total });
    }

    public async Task<ApiResult<int>> GetPendingCountAsync(long orgId, string carrier)
    {
        var count = await _db.Set<QlShentongQualityEvent>()
            .Where(e => e.FOrgId == orgId && e.F承运商 == carrier
                        && (e.F员工匹配状态 == 0 || e.F员工匹配状态 == 3)
                        && e.F员工姓名原文 != null && e.F员工姓名原文 != "")
            .Select(e => new { e.F员工姓名原文, e.F网点编码 })
            .Distinct()
            .CountAsync();
        return ApiResult<int>.Success(count);
    }
}
