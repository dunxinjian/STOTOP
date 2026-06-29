using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Services.Unification;

/// <summary>
/// 归一服务实现。C0 落地「事件类（物流完整性明细）→ 质量事件」的 worked example，
/// 确立后续所有事件类源的统一写入模式：
/// 主数据匹配（网点按名称、员工按工号/别名/启发式）→ 问题字典查/自建 → 按唯一键 upsert 质量事件。
///
/// 关键约束：
/// - 员工启发式候选（Status==3）<b>不绑工号/ID</b>，只保留姓名原文（评审强调，避免错绑）。
/// - 幂等：质量事件按唯一键 (FOrgId,F承运商,F来源STG表,F来源行ID) upsert；问题字典按唯一键
///   (FOrgId,F承运商,F质量域,F来源问题类型原文) 查/建。重复跑不增行、不重复建字典。
/// </summary>
public class QualityUnificationService : IQualityUnificationService
{
    private readonly STOTOPDbContext _db;
    private readonly IMasterDataMatcher _matcher;

    public QualityUnificationService(STOTOPDbContext db, IMasterDataMatcher matcher)
    {
        _db = db;
        _matcher = matcher;
    }

    public async global::System.Threading.Tasks.Task<UnifyResult> UnifyShentongAsync(long orgId, CancellationToken ct = default)
    {
        // 泛化分发：遍历源映射，按「目标种类 + 表名」分派到对应路径，累计五项计数。
        //
        // 路由分层（C3 地基）：
        //  - 3 个已实现的 typed 源按<b>表名精确派发</b>到各自专用方法（保留各源特有的列结构/合并语义）：
        //      物流完整性 → C0 typed、小件员履约 → C1 typed、积压监控 → C2 typed。
        //  - 其它<b>事件类</b>源 → 通用事件路径 UnifyGenericEventAsync（列名来自描述符，行用 StgRawReader raw-SQL 读）。
        //  - 其它<b>网点指标</b>源 → DispatchNetworkMetricAsync（按表名 switch 接全 10 个 NM 源：
        //      批次4 物流信息及时/完整/准确 + 揽收/出仓/滞留汇总 6 源、批次5 末端派送/签收率/拦截/渗透 4 源）。
        //  - EmployeeMetric 维持 C1（目前仅小件员履约一条）。
        // 加事件源 = 向 ShentongSourceMap.All 追加一条描述符即可，无需改本方法（通用路径自动接手）。
        int events = 0, empUpserts = 0, netUpserts = 0, netUnmatched = 0, empUnmatched = 0;

        foreach (var desc in ShentongSourceMap.All.Values)
        {
            var r = desc.TargetKind switch
            {
                // ── 事件类：3 个 typed 源按表名精确派发，其余走通用事件路径 ──
                UnifyTargetKind.Event => desc.StgTableName switch
                {
                    ShentongSourceMap.LogisticsCompletenessTable => await UnifyLogisticsCompletenessAsync(orgId, desc, ct),
                    _ => await UnifyGenericEventAsync(orgId, desc, ct),
                },

                // ── 员工指标类：维持 C1（小件员履约 typed）──
                UnifyTargetKind.EmployeeMetric => await UnifyCourierFulfillAsync(orgId, desc, ct),

                // ── 网点指标类：积压监控 typed，其余 → DispatchNetworkMetricAsync 按表名接全 10 个 NM 源 ──
                UnifyTargetKind.NetworkMetric => desc.StgTableName switch
                {
                    ShentongSourceMap.BacklogMonitorTable => await UnifyBacklogMonitorAsync(orgId, desc, ct),
                    _ => await DispatchNetworkMetricAsync(orgId, desc, ct),
                },

                _ => throw new NotSupportedException($"未支持的归一目标种类 {desc.TargetKind}（源 {desc.StgTableName}）"),
            };
            events += r.EventsUpserted;
            empUpserts += r.EmployeeMetricUpserts;
            netUpserts += r.NetworkMetricUpserts;
            netUnmatched += r.NetworkUnmatched;
            empUnmatched += r.EmployeeUnmatched;

            // 规模化内存加固：每处理完一个源即清空变更跟踪，避免整轮 29 源的 tracked 实体累积。
            // 安全：每源方法返回前已 SaveChanges；跨源网点指标合并靠"查库找已保存行"而非跟踪器；
            // 每源 dedup 缓存是方法内局部变量。故此处 Clear 不丢数据、不破合并/幂等。
            _db.ChangeTracker.Clear();
        }

        return new UnifyResult(events, empUpserts, netUpserts, netUnmatched, empUnmatched);
    }

    /// <summary>
    /// 重跑回填：<b>仅重解析「未匹配」历史质量事件的主数据，不重建事件、不重导</b>（C4）。
    ///
    /// 取 (FOrgId==orgId &amp;&amp; F承运商=="申通") 且 (F网点匹配状态==0 或 F员工匹配状态∈{0,3}) 的事件实体（可写，不投影），逐条重解析：
    ///   - 网点未匹配(0)：用事件存的 <see cref="QlShentongQualityEvent.F网点名称"/> 原文重 <see cref="IMasterDataMatcher.ResolveNetworkAsync"/>(null, 名称)；
    ///     现命中(Status==1)→回填 F网点编码、F网点匹配状态（网点编码用于下游员工启发式上下文）。
    ///   - 员工未匹配/启发式(0/3)：用 F员工姓名原文（+ 若 F员工工号 非空也带上）重 <see cref="IMasterDataMatcher.ResolveEmployeeAsync"/>(工号, 姓名, 新网点编码)；
    ///     现 Status∈{1,2}（工号/别名确定绑定）→回填 F员工工号/F员工ID/F员工匹配状态；
    ///     仍 0/3（含启发式候选）→ <b>不动</b>，保持只留姓名原文口径（与归一一致，启发式候选不绑）。
    /// 只对真正变化的行计数；EF 变更跟踪只 update 变化列。一次 SaveChanges。幂等：补别名后第一次回填，之后无变化。
    /// </summary>
    public async global::System.Threading.Tasks.Task<RematchResult> RematchUnresolvedAsync(long orgId, CancellationToken ct = default)
    {
        // 取可写实体（非投影）：候选 = 网点未匹配(0) 或 员工未匹配/启发式(0/3)。
        var candidates = await _db.Set<QlShentongQualityEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.FOrgId == orgId
                        && e.F承运商 == "申通"
                        && (e.F网点匹配状态 == 0 || e.F员工匹配状态 == 0 || e.F员工匹配状态 == 3))
            .ToListAsync(ct);

        int networkRebound = 0, employeeRebound = 0;

        foreach (var ev in candidates)
        {
            // ── 网点重解析（仅当前未匹配）：用事件存的网点名称原文重匹配 ──
            // 注意：事件当前网点编码（可能是历史回退原文）不作匹配入参，统一靠名称重解析，避免脏码干扰。
            // 已知局限：固定传 null code，仅按名称重解析——编码型源未匹配时编码已为 null（主数据通常先于导入就位，
            //   首次归一即按编码命中；落到这里的未匹配多是名称型源），故重解析只需名称即可，不丢可命中行。
            var netCode = ev.F网点编码;
            if (ev.F网点匹配状态 == 0)
            {
                var net = await _matcher.ResolveNetworkAsync(null, ev.F网点名称, orgId, ct);
                if (net.Status == 1)
                {
                    ev.F网点编码 = net.Code;
                    ev.F网点匹配状态 = 1;
                    netCode = net.Code;   // 供下游员工启发式上下文使用新网点编码
                    networkRebound++;
                }
            }

            // ── 员工重解析（仅当前未匹配/启发式 0/3，且本事件有员工维度——状态9 不适用不在候选内）──
            if (ev.F员工匹配状态 is 0 or 3)
            {
                // 用姓名原文（+ 已有工号若非空）重解析；网点编码取上面可能刚回填的新值。
                var emp = await _matcher.ResolveEmployeeAsync(ev.F员工工号, ev.F员工姓名原文, netCode, orgId, ct);
                if (emp.Status is 1 or 2)
                {
                    ev.F员工工号 = emp.EmployeeNo;
                    ev.F员工ID = emp.EmployeeId;
                    ev.F员工匹配状态 = emp.Status;
                    employeeRebound++;
                }
                // 仍 0/3（含启发式候选）→ 不动：保持只留姓名原文口径（与归一一致，启发式候选不绑工号）。
            }
        }

        if (networkRebound > 0 || employeeRebound > 0)
            await _db.SaveChangesAsync(ct);

        return new RematchResult(networkRebound, employeeRebound, candidates.Count);
    }

    /// <summary>
    /// 枚举「有申通数据可归一」的组织ID（去重）：质量事件表 ∪ 各申通 STG 源表的 distinct FOrgId。
    /// 表名全部来自静态 <see cref="ShentongSourceMap.All"/>（非用户输入，列名固定 FOrgId），仍按
    /// INFORMATION_SCHEMA 守卫「表存在 + 含 FOrgId 列」后才纳入 UNION——避免某 STG 表尚未建（首次导入前）时整查询报错。
    /// </summary>
    public async global::System.Threading.Tasks.Task<IReadOnlyList<long>> ListShentongOrgIdsAsync(CancellationToken ct = default)
    {
        // 候选表：事件表 + 全部申通 STG 源表（去重）。表名取自静态源映射，安全。
        var tables = new List<string> { "QL申通_承运商质量事件" };
        tables.AddRange(ShentongSourceMap.All.Values.Select(d => d.StgTableName));
        var distinctTables = tables.Distinct().ToList();

        var conn = _db.Database.GetDbConnection();
        bool openedHere = false;
        if (conn.State != global::System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
            openedHere = true;
        }

        try
        {
            // ── 守卫：仅保留「存在且含 FOrgId 列」的表（按 INFORMATION_SCHEMA.COLUMNS）──
            var existing = new List<string>();
            await using (var probe = conn.CreateCommand())
            {
                var inList = string.Join(",", distinctTables.Select((_, i) => $"@t{i}"));
                probe.CommandText =
                    $"SELECT DISTINCT TABLE_NAME FROM INFORMATION_SCHEMA.COLUMNS " +
                    $"WHERE COLUMN_NAME = N'FOrgId' AND TABLE_NAME IN ({inList})";
                for (int i = 0; i < distinctTables.Count; i++)
                {
                    var p = probe.CreateParameter();
                    p.ParameterName = $"@t{i}";
                    p.Value = distinctTables[i];
                    probe.Parameters.Add(p);
                }
                await using var rd = await probe.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                    existing.Add(rd.GetString(0));
            }

            if (existing.Count == 0)
                return global::System.Array.Empty<long>();

            // ── UNION 各存在表的 distinct FOrgId（表名来自静态白名单，已按存在性过滤）──
            var unionSql = string.Join(" UNION ", existing.Select(t => $"SELECT DISTINCT [FOrgId] FROM [{t}]"));
            var orgIds = new List<long>();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = unionSql;
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    if (!rd.IsDBNull(0))
                        orgIds.Add(global::System.Convert.ToInt64(rd.GetValue(0)));
                }
            }

            return orgIds.Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
        }
        finally
        {
            if (openedHere && conn.State == global::System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    /// <summary>
    /// 事件类 worked example：STG申通_物流完整性明细 → QL申通_承运商质量事件。
    /// </summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyLogisticsCompletenessAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        // 只投影需要的列（避免读全实体时碰到 STG 表里允许 NULL、但实体声明为非空 long 的系统列，
        // 如 F账套ID NULL → SqlNullValueException）。投影后即纯内存对象，匹配/写入都基于它。
        var rows = await _db.Set<StgShentongLogisticsCompleteness>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new LogisticsRow(
                r.FID, r.F批次ID, r.F统计日期, r.F运单号, r.F网点名称, r.F所属网点名称,
                r.F问题类型, r.F订单平台, r.F签收员编号, r.F签收员名称))
            .ToListAsync(ct);

        int events = 0, networkUnmatched = 0, employeeUnmatched = 0;

        // 本次运行内的字典缓存：同一原文只查/建一次（避免逐行重复查库，且同批多行同问题不会重复建）
        var dictCache = new Dictionary<string, QlShentongProblemDict>();

        foreach (var row in rows)
        {
            // ── 主数据匹配：网点（本源无编码，传名称）→ 员工（带网点编码上下文）──
            var net = await _matcher.ResolveNetworkAsync(null, row.F网点名称, orgId, ct);
            var emp = await _matcher.ResolveEmployeeAsync(row.F签收员编号, row.F签收员名称, net.Code, orgId, ct);

            // ── 问题字典查/建（按唯一键，从数据自动长出，人工后续完善）──
            var problemRaw = (row.F问题类型 ?? "").Trim();
            var dict = await GetOrCreateProblemDictAsync(orgId, desc, problemRaw, dictCache, ct);

            // ── upsert 质量事件（唯一键：来源STG表 + 来源行ID）──
            var existing = await _db.Set<QlShentongQualityEvent>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e =>
                    e.FOrgId == orgId &&
                    e.F承运商 == "申通" &&
                    e.F来源STG表 == desc.StgTableName &&
                    e.F来源行ID == row.FID, ct);

            var ev = existing ?? new QlShentongQualityEvent();

            ev.FOrgId = orgId;
            ev.F承运商 = "申通";
            ev.F业务日期 = ParseUtil.TryDate(row.F统计日期);
            ev.F统计年月 = ParseUtil.Ym(ev.F业务日期);
            ev.F运单号 = row.F运单号;

            // 网点：编码取匹配结果，名称保留原文，状态回填
            ev.F网点编码 = net.Code;
            ev.F网点名称 = row.F网点名称;
            ev.F网点匹配状态 = net.Status;

            // 员工：Status 1/2 绑工号+ID；Status 3（启发式候选）不绑，仅保留姓名原文
            if (emp.Status is 1 or 2)
            {
                ev.F员工工号 = emp.EmployeeNo;
                ev.F员工ID = emp.EmployeeId;
            }
            else
            {
                ev.F员工工号 = null;
                ev.F员工ID = null;
            }
            ev.F员工姓名原文 = row.F签收员名称;
            ev.F员工匹配状态 = emp.Status;

            ev.F电商平台 = row.F订单平台;
            ev.F质量域 = desc.QualityDomain;

            // 问题类型 / 严重度 / 是否考核 取字典
            ev.F问题类型编码 = dict.F问题类型编码;
            ev.F问题类型名称 = dict.F问题类型名称;
            ev.F严重度 = dict.F默认严重度;
            ev.F是否考核件 = dict.F是否考核;

            ev.F来源STG表 = desc.StgTableName;
            ev.F来源行ID = row.FID;
            ev.F来源批次ID = row.F批次ID;
            ev.F关键字段JSON = BuildKeySnapshot(row);

            if (existing == null)
            {
                ev.F创建时间 = DateTime.Now;
                _db.Set<QlShentongQualityEvent>().Add(ev);
            }

            events++;
            if (net.Status == 0) networkUnmatched++;
            if (emp.Status is 0 or 3) employeeUnmatched++;
        }

        await _db.SaveChangesAsync(ct);

        return new UnifyResult(
            EventsUpserted: events,
            EmployeeMetricUpserts: 0,
            NetworkMetricUpserts: 0,
            NetworkUnmatched: networkUnmatched,
            EmployeeUnmatched: employeeUnmatched);
    }

    /// <summary>
    /// 通用事件路径（C3 地基）：任意「事件类」STG 源 → QL申通_承运商质量事件。
    /// 逻辑<b>镜像 C0 <see cref="UnifyLogisticsCompletenessAsync"/></b>，差别只在于：列名全部来自 <paramref name="desc"/>，
    /// 行用 <see cref="StgRawReader"/> raw-SQL 按列名读（而非 C0 的静态 EF 投影）。
    /// 故所有口径（员工 Status 1/2 绑工号/Status 3 不绑只留姓名/Status 9 不适用全空；
    /// 计数 EventsUpserted/NetworkUnmatched/EmployeeUnmatched 含义；按唯一键 upsert 幂等）与 C0 完全一致。
    /// </summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyGenericEventAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var reader = new StgRawReader(_db);

        // ── 组装需读列：系统列 FID/F批次ID + 描述符声明的各业务列 + 关键字段快照列，去重去 null ──
        var needCols = new List<string?>
        {
            "FID", "F批次ID",
            desc.NetworkCodeColumn, desc.NetworkNameColumn,
            desc.EmployeeNoColumn, desc.EmployeeNameColumn,
            desc.ProblemTypeColumn, desc.DateColumn,
            desc.WaybillColumn, desc.PlatformColumn,
            desc.AmountColumn, desc.OnlyFilterColumn,
        };
        // 关键字段快照列：描述符显式给了就用它，否则取本源全部声明的关键业务列（与 needCols 同源，下面统一去重）。
        var snapshotCols = (desc.KeySnapshotColumns != null && desc.KeySnapshotColumns.Length > 0)
            ? desc.KeySnapshotColumns
            : new[]
            {
                desc.DateColumn, desc.WaybillColumn, desc.NetworkCodeColumn, desc.NetworkNameColumn,
                desc.ProblemTypeColumn, desc.PlatformColumn, desc.EmployeeNoColumn, desc.EmployeeNameColumn,
            }.Where(c => c != null).Select(c => c!).ToArray();
        needCols.AddRange(snapshotCols);

        var readCols = needCols.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();

        // ── raw-SQL 读行（带「仅X」过滤：两列任一为 null 则不过滤、整源入事件）──
        var rows = await reader.ReadAsync(
            desc.StgTableName, readCols, orgId,
            desc.OnlyFilterColumn, desc.OnlyFilterEquals, ct);

        int events = 0, networkUnmatched = 0, employeeUnmatched = 0;

        // 本次运行内的字典缓存：同一原文只查/建一次。
        var dictCache = new Dictionary<string, QlShentongProblemDict>();

        foreach (var row in rows)
        {
            var fid = ToLong(Get(row, "FID"));
            var batchId = ToLong(Get(row, "F批次ID"));

            // ── 主数据匹配：网点（编码/名称按描述符列取，可能其一为 null）──
            var netCodeRaw = desc.NetworkCodeColumn != null ? Get(row, desc.NetworkCodeColumn) : null;
            var netNameRaw = desc.NetworkNameColumn != null ? Get(row, desc.NetworkNameColumn) : null;
            var net = await _matcher.ResolveNetworkAsync(netCodeRaw, netNameRaw, orgId, ct);

            // ── 员工匹配：无员工维度（两列都 null）→ Status 9 不适用；否则按编号/姓名解析 ──
            EmployeeMatch emp;
            string? empNameRaw = null;
            if (desc.EmployeeNoColumn == null && desc.EmployeeNameColumn == null)
            {
                emp = new EmployeeMatch(null, null, null, 9);
            }
            else
            {
                var empNo = desc.EmployeeNoColumn != null ? Get(row, desc.EmployeeNoColumn) : null;
                empNameRaw = desc.EmployeeNameColumn != null ? Get(row, desc.EmployeeNameColumn) : null;
                emp = await _matcher.ResolveEmployeeAsync(empNo, empNameRaw, net.Code, orgId, ct);
            }

            // ── 问题类型原文：列优先（列非 null 取列），否则用常量；列单元格为 NULL 也兜底为 ""（脏数据常态，勿 NRE 整批回滚）；Trim ──
            var problemRaw = ((desc.ProblemTypeColumn != null
                ? Get(row, desc.ProblemTypeColumn)
                : desc.ProblemTypeConstant) ?? "").Trim();
            var dict = await GetOrCreateProblemDictAsync(orgId, desc, problemRaw, dictCache, ct);

            // ── upsert 质量事件（唯一键：FOrgId × 承运商 × 来源STG表 × 来源行ID）──
            var existing = await _db.Set<QlShentongQualityEvent>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e =>
                    e.FOrgId == orgId &&
                    e.F承运商 == "申通" &&
                    e.F来源STG表 == desc.StgTableName &&
                    e.F来源行ID == fid, ct);

            var ev = existing ?? new QlShentongQualityEvent();

            ev.FOrgId = orgId;
            ev.F承运商 = "申通";
            ev.F业务日期 = desc.DateColumn != null ? ParseUtil.TryDate(Get(row, desc.DateColumn)) : null;
            ev.F统计年月 = ParseUtil.Ym(ev.F业务日期);
            ev.F运单号 = desc.WaybillColumn != null ? Get(row, desc.WaybillColumn) : null;

            // 网点：编码取匹配结果，名称保留原文，状态回填
            ev.F网点编码 = net.Code;
            ev.F网点名称 = netNameRaw;
            ev.F网点匹配状态 = net.Status;

            // 员工：Status 1/2 绑工号+ID；Status 3（启发式候选）不绑，仅留姓名原文；Status 9（不适用）全空。
            if (emp.Status is 1 or 2)
            {
                ev.F员工工号 = emp.EmployeeNo;
                ev.F员工ID = emp.EmployeeId;
            }
            else
            {
                ev.F员工工号 = null;
                ev.F员工ID = null;
            }
            ev.F员工姓名原文 = empNameRaw;   // Status 9 时 empNameRaw 本就为 null
            ev.F员工匹配状态 = emp.Status;

            ev.F电商平台 = desc.PlatformColumn != null ? Get(row, desc.PlatformColumn) : null;
            ev.F质量域 = desc.QualityDomain;

            ev.F问题类型编码 = dict.F问题类型编码;
            ev.F问题类型名称 = dict.F问题类型名称;
            ev.F严重度 = dict.F默认严重度;
            ev.F是否考核件 = dict.F是否考核;

            // 金额：有金额列才填（TryDecimal 容错，解析失败 → null）。
            ev.F考核金额 = desc.AmountColumn != null ? ParseUtil.TryDecimal(Get(row, desc.AmountColumn)) : null;

            ev.F来源STG表 = desc.StgTableName;
            ev.F来源行ID = fid;
            ev.F来源批次ID = batchId;
            ev.F关键字段JSON = BuildGenericKeySnapshot(row, snapshotCols);

            if (existing == null)
            {
                ev.F创建时间 = DateTime.Now;
                _db.Set<QlShentongQualityEvent>().Add(ev);
            }

            events++;
            if (net.Status == 0) networkUnmatched++;
            if (emp.Status is 0 or 3) employeeUnmatched++;   // 与 C0 一致：状态 9 不计未匹配，0/3 计
        }

        await _db.SaveChangesAsync(ct);

        return new UnifyResult(
            EventsUpserted: events,
            EmployeeMetricUpserts: 0,
            NetworkMetricUpserts: 0,
            NetworkUnmatched: networkUnmatched,
            EmployeeUnmatched: employeeUnmatched);
    }

    /// <summary>
    /// 网点指标类「其它源」分发（积压监控走 C2 typed，其余 NM 源按表名 switch 到各自 typed 子集合并 upsert）。
    /// C3 批次4 已填 6 源、批次5 再填 4 源（末端派送/签收率/拦截/渗透），至此 NM 源全覆盖。
    /// </summary>
    private global::System.Threading.Tasks.Task<UnifyResult> DispatchNetworkMetricAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        return desc.StgTableName switch
        {
            // ── C3 批次4：6 个网点指标源，逐源 typed 子集合并 upsert（照 UnifyBacklogMonitorAsync 范式）──
            ShentongSourceMap.InfoIndexTimelyTable => UnifyInfoIndexTimelyAsync(orgId, desc, ct),
            ShentongSourceMap.InfoIndexCompleteTable => UnifyInfoIndexCompleteAsync(orgId, desc, ct),
            ShentongSourceMap.InfoIndexAccurateTable => UnifyInfoIndexAccurateAsync(orgId, desc, ct),
            ShentongSourceMap.PickupAssessTable => UnifyPickupAssessAsync(orgId, desc, ct),
            ShentongSourceMap.OutboundAssessTable => UnifyOutboundAssessAsync(orgId, desc, ct),
            ShentongSourceMap.HandoverSummaryTable => UnifyHandoverSummaryAsync(orgId, desc, ct),

            // ── C3 批次5：4 个网点指标源（最后一批源），同范式逐源 typed 子集合并 upsert ──
            ShentongSourceMap.DeliveryNetSummaryTable => UnifyDeliveryNetSummaryAsync(orgId, desc, ct),
            ShentongSourceMap.SignRateAssessTable => UnifySignRateAssessAsync(orgId, desc, ct),
            ShentongSourceMap.InterceptSummaryTable => UnifyInterceptSummaryAsync(orgId, desc, ct),
            ShentongSourceMap.PenetrationTable => UnifyPenetrationAsync(orgId, desc, ct),

            _ => throw new NotSupportedException(
                $"网点指标源「{desc.StgTableName}」尚未实现"),
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // C3 批次4：6 个网点指标源 typed 方法。每源照 UnifyBacklogMonitorAsync 范式：
    //   投影读 STG → 网点匹配 → 业务日期 .Date 截断 → 同批字典去重 + 合并 upsert（只刷本源子集）。
    // 6 源字段子集互不相交：
    //   及时 → F揽收/派件/签收上传不及时率；完整 → F揽收/派件/到件缺失率；准确 → F不准确率/F到件不准确率；
    //   揽收考核 → F及时揽收率/F未及时揽收量；出仓考核 → F一频次出仓及时率/F未及时出仓量/F出仓预估考核金额；
    //   滞留汇总 → F滞留率/F考核滞留量/F滞留预估考核金额。
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>① 物流信息及时汇总 → 网点日指标（子集：揽收/派件/签收上传不及时率）。本源<b>无网点编码列，仅有名称</b>。</summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyInfoIndexTimelyAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var rows = await _db.Set<StgShentongInfoIndexTimely>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new InfoTimelyRow(
                r.FID, r.F批次ID, r.F网点名称, r.F统计日期,
                r.F揽收上传不及时率, r.F派件上传不及时率, r.F签收上传不及时率))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            // 本源无网点编码列，仅有名称 → 按名称匹配；匹配到用 net.Code，未匹配回退源名称原文（保证唯一键稳定、不丢行）。
            var net = await _matcher.ResolveNetworkAsync(null, row.F网点名称, orgId, ct);
            var netCode = net.Status == 1 ? net.Code : (row.F网点名称 ?? "");
            if (net.Status == 0) networkUnmatched++;

            var bizDate = ParseUtil.TryDate(row.F统计日期)?.Date;
            if (bizDate == null || string.IsNullOrEmpty(netCode)) { skipped++; continue; }

            var (metric, isNew) = await GetOrAddNetworkMetricAsync(orgId, bizDate.Value, netCode!, metricCache, ct);
            ApplyKeyAndCommonDims(metric, orgId, bizDate.Value, netCode!,
                networkName: row.F网点名称, area: null, province: null, batchId: row.F批次ID);

            // 本源负责子集：物流信息上传不及时率（率存百分数；源列带 % 由 TryDecimal 仅去符号不除100）。
            metric.F揽收上传不及时率 = ParseUtil.TryDecimal(row.F揽收上传不及时率);
            metric.F派件上传不及时率 = ParseUtil.TryDecimal(row.F派件上传不及时率);
            metric.F签收上传不及时率 = ParseUtil.TryDecimal(row.F签收上传不及时率);
            // 其它 NM 源字段（完整/准确/揽收/出仓/滞留/积压…）一律不碰，保持既有值。

            FinishUpsert(metric, isNew);
            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);
        return new UnifyResult(0, 0, netUpserts, networkUnmatched, 0);
    }

    /// <summary>② 物流信息完整汇总 → 网点日指标（子集：揽收/派件/到件缺失率）。本源<b>无网点编码列，仅有名称</b>。</summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyInfoIndexCompleteAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var rows = await _db.Set<StgShentongInfoIndexComplete>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new InfoCompleteRow(
                r.FID, r.F批次ID, r.F网点名称, r.F统计日期,
                r.F揽收缺失率, r.F派件缺失率, r.F到件缺失率))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            var net = await _matcher.ResolveNetworkAsync(null, row.F网点名称, orgId, ct);
            var netCode = net.Status == 1 ? net.Code : (row.F网点名称 ?? "");
            if (net.Status == 0) networkUnmatched++;

            var bizDate = ParseUtil.TryDate(row.F统计日期)?.Date;
            if (bizDate == null || string.IsNullOrEmpty(netCode)) { skipped++; continue; }

            var (metric, isNew) = await GetOrAddNetworkMetricAsync(orgId, bizDate.Value, netCode!, metricCache, ct);
            ApplyKeyAndCommonDims(metric, orgId, bizDate.Value, netCode!,
                networkName: row.F网点名称, area: null, province: null, batchId: row.F批次ID);

            // 本源负责子集：缺失率。
            metric.F揽收缺失率 = ParseUtil.TryDecimal(row.F揽收缺失率);
            metric.F派件缺失率 = ParseUtil.TryDecimal(row.F派件缺失率);
            metric.F到件缺失率 = ParseUtil.TryDecimal(row.F到件缺失率);

            FinishUpsert(metric, isNew);
            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);
        return new UnifyResult(0, 0, netUpserts, networkUnmatched, 0);
    }

    /// <summary>③ 物流信息准确汇总 → 网点日指标（子集：不准确率/到件不准确率）。本源<b>无网点编码列，仅有名称</b>。</summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyInfoIndexAccurateAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var rows = await _db.Set<StgShentongInfoIndexAccurate>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new InfoAccurateRow(
                r.FID, r.F批次ID, r.F网点名称, r.F统计日期,
                r.F不准确率, r.F到件不准确率))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            var net = await _matcher.ResolveNetworkAsync(null, row.F网点名称, orgId, ct);
            var netCode = net.Status == 1 ? net.Code : (row.F网点名称 ?? "");
            if (net.Status == 0) networkUnmatched++;

            var bizDate = ParseUtil.TryDate(row.F统计日期)?.Date;
            if (bizDate == null || string.IsNullOrEmpty(netCode)) { skipped++; continue; }

            var (metric, isNew) = await GetOrAddNetworkMetricAsync(orgId, bizDate.Value, netCode!, metricCache, ct);
            ApplyKeyAndCommonDims(metric, orgId, bizDate.Value, netCode!,
                networkName: row.F网点名称, area: null, province: null, batchId: row.F批次ID);

            // 本源负责子集：准确率。
            metric.F不准确率 = ParseUtil.TryDecimal(row.F不准确率);
            metric.F到件不准确率 = ParseUtil.TryDecimal(row.F到件不准确率);

            FinishUpsert(metric, isNew);
            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);
        return new UnifyResult(0, 0, netUpserts, networkUnmatched, 0);
    }

    /// <summary>④ 揽收考核汇总 → 网点日指标（子集：及时揽收率/未及时揽收量）。本源<b>有网点编码</b>（F揽收所属网点编码）。</summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyPickupAssessAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var rows = await _db.Set<StgShentongPickupAssess>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new PickupAssessRow(
                r.FID, r.F批次ID, r.F揽收所属网点编码, r.F揽收所属网点, r.F统计日期, r.F揽收省区,
                r.F及时揽收率, r.F未及时揽收量))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            // 本源有网点编码（带名称兜底）；匹配到用 net.Code，未匹配回退源编码原文。
            var net = await _matcher.ResolveNetworkAsync(row.F揽收所属网点编码, row.F揽收所属网点, orgId, ct);
            var netCode = net.Status == 1 ? net.Code : (row.F揽收所属网点编码 ?? "");
            if (net.Status == 0) networkUnmatched++;

            var bizDate = ParseUtil.TryDate(row.F统计日期)?.Date;
            if (bizDate == null || string.IsNullOrEmpty(netCode)) { skipped++; continue; }

            var (metric, isNew) = await GetOrAddNetworkMetricAsync(orgId, bizDate.Value, netCode!, metricCache, ct);
            ApplyKeyAndCommonDims(metric, orgId, bizDate.Value, netCode!,
                networkName: row.F揽收所属网点, area: null, province: row.F揽收省区, batchId: row.F批次ID);

            // 本源负责子集：揽收（率存百分数；源「及时揽收率」无尾缀纯数值如 99.68 原样解析，未及时量整数）。
            metric.F及时揽收率 = ParseUtil.TryDecimal(row.F及时揽收率);
            metric.F未及时揽收量 = ParseUtil.TryInt(row.F未及时揽收量);

            FinishUpsert(metric, isNew);
            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);
        return new UnifyResult(0, 0, netUpserts, networkUnmatched, 0);
    }

    /// <summary>⑤ 出仓考核汇总 → 网点日指标（子集：一频次出仓及时率/未及时出仓量/出仓预估考核金额）。本源<b>有网点编码</b>（F所属网点编码）。</summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyOutboundAssessAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var rows = await _db.Set<StgShentongOutboundAssess>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new OutboundAssessRow(
                r.FID, r.F批次ID, r.F所属网点编码, r.F所属网点, r.F统计日期, r.F片区,
                r.F一频次考核出仓及时率, r.F一频次考核未及时出仓量, r.F一频次考核预估考核金额元))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            var net = await _matcher.ResolveNetworkAsync(row.F所属网点编码, row.F所属网点, orgId, ct);
            var netCode = net.Status == 1 ? net.Code : (row.F所属网点编码 ?? "");
            if (net.Status == 0) networkUnmatched++;

            var bizDate = ParseUtil.TryDate(row.F统计日期)?.Date;
            if (bizDate == null || string.IsNullOrEmpty(netCode)) { skipped++; continue; }

            var (metric, isNew) = await GetOrAddNetworkMetricAsync(orgId, bizDate.Value, netCode!, metricCache, ct);
            ApplyKeyAndCommonDims(metric, orgId, bizDate.Value, netCode!,
                networkName: row.F所属网点, area: row.F片区, province: null, batchId: row.F批次ID);

            // 本源负责子集：出仓（一频次=考核频次；率带 % 由 TryDecimal 去符号；金额 TryDecimal）。
            metric.F一频次出仓及时率 = ParseUtil.TryDecimal(row.F一频次考核出仓及时率);
            metric.F未及时出仓量 = ParseUtil.TryInt(row.F一频次考核未及时出仓量);
            metric.F出仓预估考核金额 = ParseUtil.TryDecimal(row.F一频次考核预估考核金额元);

            FinishUpsert(metric, isNew);
            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);
        return new UnifyResult(0, 0, netUpserts, networkUnmatched, 0);
    }

    /// <summary>⑥ 交货滞留汇总 → 网点日指标（子集：滞留率/考核滞留量/滞留预估考核金额）。本源<b>有网点编码</b>（F揽收所属网点编码）。
    /// 注：草案「滞留预估考核金额」在 STG 真名为 <c>F滞留预估考核日</c>（导出列「滞留预估考核-日」去连字符），语义即滞留预估考核金额，映射到 QL <c>F滞留预估考核金额</c>。</summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyHandoverSummaryAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var rows = await _db.Set<StgShentongHandoverSummary>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new HandoverSummaryRow(
                r.FID, r.F批次ID, r.F揽收所属网点编码, r.F揽收网点所属网点, r.F统计日期, r.F揽收网点省区,
                r.F滞留率, r.F考核滞留量, r.F滞留预估考核日))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            var net = await _matcher.ResolveNetworkAsync(row.F揽收所属网点编码, row.F揽收网点所属网点, orgId, ct);
            var netCode = net.Status == 1 ? net.Code : (row.F揽收所属网点编码 ?? "");
            if (net.Status == 0) networkUnmatched++;

            var bizDate = ParseUtil.TryDate(row.F统计日期)?.Date;
            if (bizDate == null || string.IsNullOrEmpty(netCode)) { skipped++; continue; }

            var (metric, isNew) = await GetOrAddNetworkMetricAsync(orgId, bizDate.Value, netCode!, metricCache, ct);
            ApplyKeyAndCommonDims(metric, orgId, bizDate.Value, netCode!,
                networkName: row.F揽收网点所属网点, area: null, province: row.F揽收网点省区, batchId: row.F批次ID);

            // 本源负责子集：滞留（滞留率带 % 或纯数值由 TryDecimal 处理；考核滞留量整数；滞留预估考核日＝滞留预估考核金额）。
            metric.F滞留率 = ParseUtil.TryDecimal(row.F滞留率);
            metric.F考核滞留量 = ParseUtil.TryInt(row.F考核滞留量);
            metric.F滞留预估考核金额 = ParseUtil.TryDecimal(row.F滞留预估考核日);

            FinishUpsert(metric, isNew);
            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);
        return new UnifyResult(0, 0, netUpserts, networkUnmatched, 0);
    }

    // ─────────────────────────────────────────────────────────────────────
    // C3 批次5：4 个网点指标源 typed 方法（最后一批源）。同批次4 范式：
    //   投影读 STG → 网点匹配 → 业务日期 .Date 截断 → 同批字典去重 + 合并 upsert（只刷本源子集）。
    // 4 源字段子集互不相交：
    //   末端派送汇总 → 一/二阶段/当天及时签收率 + 派送预估考核金额/有偿派费金额/预计返款金额；
    //   签收率考核汇总 → 仅 F签收率考核金额（与末端派送汇总互不相交）；
    //   拦截汇总 → 应拦截量/拦截成功率/及时转出率；
    //   渗透建站考核 → 自建渗透率/渗透率目标/建站待完成/喵柜激活格口数。
    // 关键不变量（签收域两源不抢字段）：末端派送汇总与签收率考核汇总同属「派送签收」域、会落到同一 (网点×日) 行时，
    //   各只刷自己的字段子集（前者不碰 F签收率考核金额，后者不碰签收率/派费/返款），互不清空。
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>① 末端派送网点汇总 → 网点日指标（签收域子集：一/二阶段/当天及时签收率 + 派送预估考核金额/有偿派费金额/预计返款金额）。
    /// 本源<b>无网点编码列，仅有名称 F应签所属网点</b>（按名称匹配，未匹配回退名称原文）。
    /// 注：草案「派送预估考核金额」在 STG 真名为 <c>F预计考核金额</c>，映射到 QL <c>F派送预估考核金额</c>。
    /// 率源以小数分数落（如 0.8903＝89.03%），TryDecimal 原样解析（无尾缀×1）。
    /// 关键：<b>不碰 F签收率考核金额</b>（属签收率考核汇总源子集），避免抢字段。</summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyDeliveryNetSummaryAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var rows = await _db.Set<StgShentongDeliveryNetSummary>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new DeliveryNetSummaryRow(
                r.FID, r.F批次ID, r.F应签所属网点, r.F统计日期,
                r.F一阶段及时签收率, r.F二阶段及时签收率, r.F当天及时签收率,
                r.F预计考核金额, r.F有偿派费金额, r.F预计返款金额))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            // 本源无网点编码列，仅有名称 → 按名称匹配；匹配到用 net.Code，未匹配回退源名称原文。
            var net = await _matcher.ResolveNetworkAsync(null, row.F应签所属网点, orgId, ct);
            var netCode = net.Status == 1 ? net.Code : (row.F应签所属网点 ?? "");
            if (net.Status == 0) networkUnmatched++;

            var bizDate = ParseUtil.TryDate(row.F统计日期)?.Date;
            if (bizDate == null || string.IsNullOrEmpty(netCode)) { skipped++; continue; }

            var (metric, isNew) = await GetOrAddNetworkMetricAsync(orgId, bizDate.Value, netCode!, metricCache, ct);
            ApplyKeyAndCommonDims(metric, orgId, bizDate.Value, netCode!,
                networkName: row.F应签所属网点, area: null, province: null, batchId: row.F批次ID);

            // 本源负责子集：签收时效（三阶段及时签收率 + 派送预估考核/派费/返款）。
            metric.F一阶段及时签收率 = ParseUtil.TryDecimal(row.F一阶段及时签收率);
            metric.F二阶段及时签收率 = ParseUtil.TryDecimal(row.F二阶段及时签收率);
            metric.F当天及时签收率 = ParseUtil.TryDecimal(row.F当天及时签收率);
            metric.F派送预估考核金额 = ParseUtil.TryDecimal(row.F预计考核金额);
            metric.F有偿派费金额 = ParseUtil.TryDecimal(row.F有偿派费金额);
            metric.F预计返款金额 = ParseUtil.TryDecimal(row.F预计返款金额);
            // 关键：F签收率考核金额 / F48h签收率 属签收率考核汇总源，本源一律不碰（保持既有值，不抢字段）。

            FinishUpsert(metric, isNew);
            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);
        return new UnifyResult(0, 0, netUpserts, networkUnmatched, 0);
    }

    /// <summary>② 签收率考核汇总 → 网点日指标（签收域子集：<b>仅 F签收率考核金额</b> ← F总金额=总考核金额）。
    /// 本源<b>有网点编码</b>（F网点编号，名称 F网点名称 兜底）。日期列 F日期（yyyyMMdd，TryDate 兼容）。
    /// 草案 F48h签收率 该源无对应列（退化表头分时段明细未逐列建模）→ 留 null。
    /// 关键：<b>只刷 F签收率考核金额、不碰签收率/派费/返款</b>（属末端派送汇总源子集），避免抢字段。</summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifySignRateAssessAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var rows = await _db.Set<StgShentongSignRateAssess>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new SignRateAssessRow(
                r.FID, r.F批次ID, r.F网点编号, r.F网点名称, r.F日期, r.F所属省区, r.F总金额))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            var net = await _matcher.ResolveNetworkAsync(row.F网点编号, row.F网点名称, orgId, ct);
            var netCode = net.Status == 1 ? net.Code : (row.F网点编号 ?? "");
            if (net.Status == 0) networkUnmatched++;

            var bizDate = ParseUtil.TryDate(row.F日期)?.Date;
            if (bizDate == null || string.IsNullOrEmpty(netCode)) { skipped++; continue; }

            var (metric, isNew) = await GetOrAddNetworkMetricAsync(orgId, bizDate.Value, netCode!, metricCache, ct);
            ApplyKeyAndCommonDims(metric, orgId, bizDate.Value, netCode!,
                networkName: row.F网点名称, area: null, province: row.F所属省区, batchId: row.F批次ID);

            // 本源负责子集：仅 F签收率考核金额（← 总考核金额，金额 TryDecimal）。
            metric.F签收率考核金额 = ParseUtil.TryDecimal(row.F总金额);
            // 关键：签收率/派费/返款/48h 属末端派送汇总源（及该源无 48h 列），本源一律不碰（保持既有值，不抢字段）。

            FinishUpsert(metric, isNew);
            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);
        return new UnifyResult(0, 0, netUpserts, networkUnmatched, 0);
    }

    /// <summary>③ 拦截汇总 → 网点日指标（拦截子集：应拦截量(I)/拦截成功率(D)/及时转出率(D)）。
    /// 本源<b>无网点编码列，仅有名称 F所属网点</b>（按名称匹配，未匹配回退名称原文）。
    /// 率源带 %（如 83.87%），TryDecimal 去符号不除100。</summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyInterceptSummaryAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var rows = await _db.Set<StgShentongInterceptSummary>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new InterceptSummaryRow(
                r.FID, r.F批次ID, r.F所属网点, r.F统计日期,
                r.F应拦截量, r.F拦截成功率, r.F及时转出率))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            var net = await _matcher.ResolveNetworkAsync(null, row.F所属网点, orgId, ct);
            var netCode = net.Status == 1 ? net.Code : (row.F所属网点 ?? "");
            if (net.Status == 0) networkUnmatched++;

            var bizDate = ParseUtil.TryDate(row.F统计日期)?.Date;
            if (bizDate == null || string.IsNullOrEmpty(netCode)) { skipped++; continue; }

            var (metric, isNew) = await GetOrAddNetworkMetricAsync(orgId, bizDate.Value, netCode!, metricCache, ct);
            ApplyKeyAndCommonDims(metric, orgId, bizDate.Value, netCode!,
                networkName: row.F所属网点, area: null, province: null, batchId: row.F批次ID);

            // 本源负责子集：拦截（应拦截量整数，拦截成功率/及时转出率带 % 由 TryDecimal 去符号）。
            metric.F应拦截量 = ParseUtil.TryInt(row.F应拦截量);
            metric.F拦截成功率 = ParseUtil.TryDecimal(row.F拦截成功率);
            metric.F及时转出率 = ParseUtil.TryDecimal(row.F及时转出率);

            FinishUpsert(metric, isNew);
            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);
        return new UnifyResult(0, 0, netUpserts, networkUnmatched, 0);
    }

    /// <summary>④ 渗透建站考核 → 网点日指标（渗透子集：自建渗透率(←F已认证自建渗透率)/渗透率目标(←F自建渗透率当月目标)/建站待完成(I)/喵柜激活格口数(I)）。
    /// 本源<b>有网点编码</b>（F网点编号，名称 F网点名称 兜底）。
    /// 日期列 F统计周期 为「YYYY-第MM月」（如「2026-第06月」），<b>非标准日期 TryDate 解析不出</b>→ 用 <see cref="TryPeriodMonthFirstDay"/> 取<b>月首日</b>（2026-第06月 → 2026-06-01）。
    /// 率源带 %（如 11.61%/15.00%），TryDecimal 去符号不除100。</summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyPenetrationAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        var rows = await _db.Set<StgShentongPenetration>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new PenetrationRow(
                r.FID, r.F批次ID, r.F网点编号, r.F网点名称, r.F统计周期,
                r.F已认证自建渗透率, r.F自建渗透率当月目标, r.F建站待完成, r.F喵柜激活格口数))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            var net = await _matcher.ResolveNetworkAsync(row.F网点编号, row.F网点名称, orgId, ct);
            var netCode = net.Status == 1 ? net.Code : (row.F网点编号 ?? "");
            if (net.Status == 0) networkUnmatched++;

            // 日期列是周期文本「YYYY-第MM月」→ 取月首日；解析不出则跳过（无法构成稳定 (网点×日) 键）。
            var bizDate = TryPeriodMonthFirstDay(row.F统计周期);
            if (bizDate == null || string.IsNullOrEmpty(netCode)) { skipped++; continue; }

            var (metric, isNew) = await GetOrAddNetworkMetricAsync(orgId, bizDate.Value, netCode!, metricCache, ct);
            ApplyKeyAndCommonDims(metric, orgId, bizDate.Value, netCode!,
                networkName: row.F网点名称, area: null, province: null, batchId: row.F批次ID);

            // 本源负责子集：渗透/建站/喵柜（率带 % 由 TryDecimal 去符号；量整数）。
            metric.F自建渗透率 = ParseUtil.TryDecimal(row.F已认证自建渗透率);
            metric.F渗透率目标 = ParseUtil.TryDecimal(row.F自建渗透率当月目标);
            metric.F建站待完成 = ParseUtil.TryInt(row.F建站待完成);
            metric.F喵柜激活格口数 = ParseUtil.TryInt(row.F喵柜激活格口数);

            FinishUpsert(metric, isNew);
            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);
        return new UnifyResult(0, 0, netUpserts, networkUnmatched, 0);
    }

    /// <summary>
    /// 解析渗透源的统计周期文本「YYYY-第MM月」（如「2026-第06月」）为该月首日（2026-06-01）。
    /// 先尝试通用 <see cref="ParseUtil.TryDate"/>（万一某批次给的是标准日期/yyyy-MM），不行再正则抽「YYYY ... MM 月」取月首日；
    /// 全失败返回 null（该行 skip）。月度网点汇总落月首日，便于与日粒度行区分且键稳定。
    /// </summary>
    private static DateTime? TryPeriodMonthFirstDay(string? s)
    {
        var t = (s ?? "").Trim();
        if (string.IsNullOrEmpty(t)) return null;

        // 1) 标准日期/yyyy-MM 兜底（DateTime.TryParse 能解 "2026-06"/"2026-06-01" 等）。
        var direct = ParseUtil.TryDate(t);
        if (direct != null) return new global::System.DateTime(direct.Value.Year, direct.Value.Month, 1);

        // 2) 「YYYY-第MM月」：抽 4 位年 + 1~2 位月（容忍「第」「月」等中文修饰与各种分隔）。
        // 注：STOTOP.Module.System 模块命名空间遮蔽 BCL System，须 global:: 显式定位 BCL 类型。
        var m = global::System.Text.RegularExpressions.Regex.Match(t, @"(\d{4}).*?(\d{1,2})");
        if (m.Success
            && int.TryParse(m.Groups[1].Value, out var year)
            && int.TryParse(m.Groups[2].Value, out var month)
            && month is >= 1 and <= 12)
        {
            return new global::System.DateTime(year, month, 1);
        }
        return null;
    }

    // ── 批次5 各源行内存投影（只含归一需要列，避免读全实体碰 NULL 系统列）──
    private sealed record DeliveryNetSummaryRow(
        long FID, long F批次ID, string? F应签所属网点, string? F统计日期,
        string? F一阶段及时签收率, string? F二阶段及时签收率, string? F当天及时签收率,
        string? F预计考核金额, string? F有偿派费金额, string? F预计返款金额);

    private sealed record SignRateAssessRow(
        long FID, long F批次ID, string? F网点编号, string? F网点名称, string? F日期, string? F所属省区, string? F总金额);

    private sealed record InterceptSummaryRow(
        long FID, long F批次ID, string? F所属网点, string? F统计日期,
        string? F应拦截量, string? F拦截成功率, string? F及时转出率);

    private sealed record PenetrationRow(
        long FID, long F批次ID, string? F网点编号, string? F网点名称, string? F统计周期,
        string? F已认证自建渗透率, string? F自建渗透率当月目标, string? F建站待完成, string? F喵柜激活格口数);

    // ── 批次4 共用 helper：网点日指标合并 upsert 的同批去重查/建、键+公共维度写入、收尾 Add ──

    /// <summary>
    /// 按 (业务日期, 网点编码) 取网点日指标实体（先查同批缓存→再查库→库无则 new+Add+入缓存），返回 (实体, 是否新建)。
    /// 与 UnifyBacklogMonitorAsync 同批去重逻辑一致：避免同批同键两行各 Add 撞 DB 唯一索引整批回滚。
    /// </summary>
    private async global::System.Threading.Tasks.Task<(QlShentongNetworkDailyMetric metric, bool isNew)> GetOrAddNetworkMetricAsync(
        long orgId, DateTime bizDate, string netCode,
        Dictionary<(DateTime, string), QlShentongNetworkDailyMetric> cache, CancellationToken ct)
    {
        var key = (bizDate, netCode);
        if (cache.TryGetValue(key, out var cached)) return (cached, false);

        var existing = await _db.Set<QlShentongNetworkDailyMetric>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m =>
                m.FOrgId == orgId &&
                m.F承运商 == "申通" &&
                m.F业务日期 == bizDate &&
                m.F网点编码 == netCode, ct);

        var isNew = existing == null;
        var metric = existing ?? new QlShentongNetworkDailyMetric();
        cache[key] = metric;
        return (metric, isNew);
    }

    /// <summary>
    /// 写键 + 公共维度（多源都可写）。公共维度（网点名称/片区/省区）仅在本源有值时刷新，不清空别源已填值。
    /// </summary>
    private static void ApplyKeyAndCommonDims(
        QlShentongNetworkDailyMetric metric, long orgId, DateTime bizDate, string netCode,
        string? networkName, string? area, string? province, long batchId)
    {
        metric.FOrgId = orgId;
        metric.F承运商 = "申通";
        metric.F业务日期 = bizDate;
        metric.F统计年月 = ParseUtil.Ym(bizDate);
        metric.F网点编码 = netCode;
        if (!string.IsNullOrWhiteSpace(networkName)) metric.F网点名称 = networkName;
        if (!string.IsNullOrWhiteSpace(area)) metric.F片区 = area;
        if (!string.IsNullOrWhiteSpace(province)) metric.F省区 = province;
        metric.F来源批次ID = batchId;
    }

    /// <summary>新建实体补创建时间并 Add（既有实体只更新字段，EF 自动跟踪）。</summary>
    private void FinishUpsert(QlShentongNetworkDailyMetric metric, bool isNew)
    {
        if (isNew)
        {
            metric.F创建时间 = DateTime.Now;
            _db.Set<QlShentongNetworkDailyMetric>().Add(metric);
        }
    }

    // ── 批次4 各源行内存投影（只含归一需要列，避免读全实体碰 NULL 系统列）──
    private sealed record InfoTimelyRow(
        long FID, long F批次ID, string? F网点名称, string? F统计日期,
        string? F揽收上传不及时率, string? F派件上传不及时率, string? F签收上传不及时率);

    private sealed record InfoCompleteRow(
        long FID, long F批次ID, string? F网点名称, string? F统计日期,
        string? F揽收缺失率, string? F派件缺失率, string? F到件缺失率);

    private sealed record InfoAccurateRow(
        long FID, long F批次ID, string? F网点名称, string? F统计日期,
        string? F不准确率, string? F到件不准确率);

    private sealed record PickupAssessRow(
        long FID, long F批次ID, string? F揽收所属网点编码, string? F揽收所属网点, string? F统计日期, string? F揽收省区,
        string? F及时揽收率, string? F未及时揽收量);

    private sealed record OutboundAssessRow(
        long FID, long F批次ID, string? F所属网点编码, string? F所属网点, string? F统计日期, string? F片区,
        string? F一频次考核出仓及时率, string? F一频次考核未及时出仓量, string? F一频次考核预估考核金额元);

    private sealed record HandoverSummaryRow(
        long FID, long F批次ID, string? F揽收所属网点编码, string? F揽收网点所属网点, string? F统计日期, string? F揽收网点省区,
        string? F滞留率, string? F考核滞留量, string? F滞留预估考核日);

    /// <summary>从行字典按列名取字符串值（NULL/缺列 → null；非字符串列 ToString）。</summary>
    private static string? Get(Dictionary<string, object?> row, string col)
    {
        if (!row.TryGetValue(col, out var v) || v == null) return null;
        return v as string ?? v.ToString();
    }

    /// <summary>把行里的 long/int/字符串值安全转 long（FID/F批次ID 列）。</summary>
    private static long ToLong(object? v)
    {
        if (v == null) return 0;
        if (v is long l) return l;
        if (v is int i) return i;
        return long.TryParse(v.ToString(), out var r) ? r : 0;
    }

    /// <summary>通用关键列快照 JSON（按描述符选定列；列缺失/NULL 取 null）。</summary>
    private static string BuildGenericKeySnapshot(Dictionary<string, object?> row, string[] cols)
    {
        var snap = new Dictionary<string, string?>();
        foreach (var c in cols)
            snap[c] = Get(row, c);
        return JsonSerializer.Serialize(snap);
    }

    /// <summary>
    /// 员工指标类路径：STG申通_小件员履约指标 → QL申通_员工日质量指标。
    /// 员工级粒度（1 行/网点/小件员）。本源<b>无日期列</b>，业务日期取批次创建日；
    /// 员工<b>无工号、仅脏名</b>，仅当主数据匹配到工号（emp.Status∈{1,2}）才建指标（工号是唯一键一部分）。
    /// 唯一键 (FOrgId,F承运商,F业务日期,F网点编码,F员工工号)，按键 upsert，重复跑不增行（幂等）。
    /// </summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyCourierFulfillAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        // 只投影需要的列（避免读全实体碰 STG 表允许 NULL、实体声明非空 long 的系统列，如 F账套ID NULL → SqlNullValueException）。
        var rows = await _db.Set<StgShentongCourierFulfill>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new CourierRow(
                r.FID, r.F批次ID, r.F所属网点, r.F所属小件员,
                r.F当日派签量, r.F当日派签率, r.F应上门量, r.F未上门量, r.F按需上门率,
                r.F客诉发起量, r.F工单定责量, r.F客诉发起率,
                r.F虚假电联, r.F无效电联, r.F双签, r.F照片定位虚假, r.F签收文本不规范, r.F引导代收,
                r.F回访真实率))
            .ToListAsync(ct);

        // 一次性查本批次集合的 CfBatch.F创建时间，建 F批次ID → 批次日(.Date) 映射。
        var batchIds = rows.Select(r => r.F批次ID).Distinct().ToList();
        var batchDays = await _db.Set<CfBatch>()
            .IgnoreQueryFilters()
            .Where(b => batchIds.Contains(b.FID))
            .Select(b => new { b.FID, b.FCreatedTime })
            .ToDictionaryAsync(x => x.FID, x => x.FCreatedTime.Date, ct);

        int empUpserts = 0, networkUnmatched = 0, employeeUnmatched = 0;

        // 同批去重缓存：键 = (业务日期, 网点编码, 工号)。
        // 防止同一次归一里出现两条相同唯一键行时，第二行查库查不到第一行刚 Add 但未 SaveChanges 的待插实体，
        // 又 Add 第二个 → SaveChangesAsync 撞 DB 唯一索引 → 整批回滚。命中缓存则复用同一实例继续 upsert。
        var metricCache = new Dictionary<(DateTime, string, string), QlShentongEmployeeDailyMetric>();

        foreach (var row in rows)
        {
            // ── 主数据匹配：网点（本源无编码，传名称）→ 员工（脏名，无工号）──
            var net = await _matcher.ResolveNetworkAsync(null, row.F所属网点, orgId, ct);
            var emp = await _matcher.ResolveEmployeeAsync(null, row.F所属小件员, net.Code, orgId, ct);

            if (net.Status == 0) networkUnmatched++;

            // 业务日期 = 批次创建日。TODO：用户将让申通给该导出加日期列；届时改读源日期列（desc.DateColumn）。
            var bizDate = batchDays.TryGetValue(row.F批次ID, out var d) ? d : DateTime.Today;

            // 仅 emp.Status∈{1,2}（匹配到工号）才 upsert 员工日指标——工号是唯一键一部分，未确定工号无法建键。
            // 未匹配(Status 0/3)→ 不建指标，计入 EmployeeUnmatched；待人工认领+补别名后重跑生成。
            if (emp.Status is not (1 or 2))
            {
                employeeUnmatched++;
                continue;
            }

            // ── upsert 员工日指标（唯一键：工号 × 业务日期 × 网点编码，含 FOrgId/F承运商）──
            // 先查同批缓存（命中=本批前面已处理过同键行，复用实例，isNew=false 避免重复 Add）；
            // 未命中再查库；库无则 new+Add+入缓存。
            var cacheKey = (bizDate, net.Code!, emp.EmployeeNo!);
            bool isNew = false;
            if (!metricCache.TryGetValue(cacheKey, out var metric))
            {
                var existing = await _db.Set<QlShentongEmployeeDailyMetric>()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m =>
                        m.FOrgId == orgId &&
                        m.F承运商 == "申通" &&
                        m.F业务日期 == bizDate &&
                        m.F网点编码 == net.Code &&
                        m.F员工工号 == emp.EmployeeNo, ct);

                isNew = existing == null;
                metric = existing ?? new QlShentongEmployeeDailyMetric();
                metricCache[cacheKey] = metric;
            }

            metric.FOrgId = orgId;
            metric.F承运商 = "申通";
            metric.F业务日期 = bizDate;
            metric.F统计年月 = ParseUtil.Ym(bizDate);
            metric.F网点编码 = net.Code!;
            metric.F员工工号 = emp.EmployeeNo!;
            metric.F员工姓名原文 = row.F所属小件员;   // 脏名原文
            metric.F员工ID = emp.EmployeeId;

            // ── 派签 ──
            metric.F当日派签量 = ParseUtil.TryInt(row.F当日派签量);
            metric.F当日派签率 = ParseUtil.TryDecimal(row.F当日派签率);
            metric.F应上门量 = ParseUtil.TryInt(row.F应上门量);
            metric.F未上门量 = ParseUtil.TryInt(row.F未上门量);
            metric.F按需上门率 = ParseUtil.TryDecimal(row.F按需上门率);

            // ── 客诉 ──
            metric.F客诉发起量 = ParseUtil.TryInt(row.F客诉发起量);
            metric.F工单定责量 = ParseUtil.TryInt(row.F工单定责量);
            metric.F客诉发起率 = ParseUtil.TryDecimal(row.F客诉发起率);

            // ── 违规 ──
            metric.F违规虚假电联 = ParseUtil.TryInt(row.F虚假电联);
            metric.F违规无效电联 = ParseUtil.TryInt(row.F无效电联);
            metric.F违规双签 = ParseUtil.TryInt(row.F双签);
            metric.F违规照片定位虚假 = ParseUtil.TryInt(row.F照片定位虚假);
            metric.F违规签收文本不规范 = ParseUtil.TryInt(row.F签收文本不规范);
            metric.F违规引导代收 = ParseUtil.TryInt(row.F引导代收);
            metric.F回访真实率 = ParseUtil.TryDecimal(row.F回访真实率);

            // 事件类才有的字段（F虚假签收数/F照片质检不合格数/F派送超时T0-T3/F问题件数/F考核金额合计 等）
            // 来自事件聚合，非本源直供，本期留 null（不覆盖既有值——upsert 时仅本源字段被刷新）。

            metric.F来源批次ID = row.F批次ID;

            if (isNew)
            {
                metric.F创建时间 = DateTime.Now;
                _db.Set<QlShentongEmployeeDailyMetric>().Add(metric);
            }

            empUpserts++;
        }

        await _db.SaveChangesAsync(ct);

        return new UnifyResult(
            EventsUpserted: 0,
            EmployeeMetricUpserts: empUpserts,
            NetworkMetricUpserts: 0,
            NetworkUnmatched: networkUnmatched,
            EmployeeUnmatched: employeeUnmatched);
    }

    /// <summary>
    /// 小件员履约源行的内存投影（只含归一需要的列），避免读全实体碰 NULL 系统列。
    /// </summary>
    private sealed record CourierRow(
        long FID,
        long F批次ID,
        string? F所属网点,
        string? F所属小件员,
        string? F当日派签量,
        string? F当日派签率,
        string? F应上门量,
        string? F未上门量,
        string? F按需上门率,
        string? F客诉发起量,
        string? F工单定责量,
        string? F客诉发起率,
        string? F虚假电联,
        string? F无效电联,
        string? F双签,
        string? F照片定位虚假,
        string? F签收文本不规范,
        string? F引导代收,
        string? F回访真实率);

    /// <summary>
    /// 网点指标类路径（<b>多源合并 upsert 模板</b>）：STG申通_积压监控汇总 → QL申通_网点日质量指标。
    /// 网点级粒度（1 行/网点/日期），本源<b>有网点编码</b>。
    ///
    /// 关键不变量——网点日指标由多个 NetworkMetric 源各填子集、按 网点×日 合并：
    /// 同一 (网点×日) 行会被多个源先后 upsert，每个源<b>只刷新自己负责的字段子集</b>，
    /// <b>绝不把本源不负责的字段清成 null/整行覆盖</b>。本源负责「积压与遗失」子集
    /// （日均出/进港量、积压倍数、超3/5/7天积压量、遗失率ppm/遗失量、进港投诉量/率、虚签投诉率、7日虚签投诉量）；
    /// 出仓/滞留/签收/拦截/渗透等字段留给其它 C3 源填，本源一律不碰（保持既有值）。
    ///
    /// 网点编码（唯一键一部分）：优先用主数据匹配码 <see cref="NetworkMatch.Code"/>；未匹配则回退源 F网点编码 原文，
    /// 保证唯一键稳定且不丢行。日期解析失败的行跳过（计 skip，日志可见）。
    /// 唯一键 (FOrgId,F承运商,F业务日期,F网点编码)，按键 upsert，重复跑不增行（幂等）。
    /// </summary>
    private async global::System.Threading.Tasks.Task<UnifyResult> UnifyBacklogMonitorAsync(
        long orgId, ShentongSourceDescriptor desc, CancellationToken ct)
    {
        // 只投影需要的列（避免读全实体碰 STG 表允许 NULL、实体声明非空 long 的系统列，如 F账套ID NULL → SqlNullValueException）。
        var rows = await _db.Set<StgShentongBacklogMonitor>()
            .IgnoreQueryFilters()
            .Where(r => r.FOrgId == orgId && !r.FIsRevoked)
            .Select(r => new BacklogRow(
                r.FID, r.F批次ID, r.F网点编码, r.F网点名称, r.F统计日期, r.F片区名称, r.F省区,
                r.F日均出港量, r.F日均进港量, r.F积压倍数,
                r.F超3天积压量疑似遗失, r.F超5天积压量智能遗失, r.F超7天积压量超长单,
                r.F遗失率ppm, r.F遗失量,
                r.F进港投诉量, r.F进港投诉率, r.F虚签投诉率上一周, r.F7日虚签投诉量))
            .ToListAsync(ct);

        int netUpserts = 0, networkUnmatched = 0, skipped = 0;

        // 同批去重缓存：键 = (业务日期, 网点编码)。
        // 防止同一次归一里出现两条相同 (网点×日) 行时，第二行查库查不到第一行刚 Add 但未 SaveChanges 的待插实体，
        // 又 Add 第二个 → SaveChangesAsync 撞 DB 唯一索引 → 整批回滚。命中缓存则复用同一实例，
        // 继续走「只刷本源子集不覆盖」的合并逻辑（同批重复键合并为一行）。
        var metricCache = new Dictionary<(DateTime, string), QlShentongNetworkDailyMetric>();

        foreach (var row in rows)
        {
            // ── 网点匹配（本源有编码，按编码解析；无名称传 null）──
            var net = await _matcher.ResolveNetworkAsync(row.F网点编码, null, orgId, ct);
            // 网点编码（唯一键一部分）：匹配到用匹配码；未匹配回退源编码原文，避免丢行。
            //
            // 已知前提（C2 留作前提，不改键结构）：唯一键里「匹配码」与「未匹配回退原文」共用同一
            // F网点编码 命名空间——极端下若某行匹配到的 net.Code 恰与另一行未匹配回退的 F网点编码 原文字面相同，
            // 二者会被当作同一 (网点×日) 行合并。当前积压监控恒带网点编码、基本命中匹配，字面撞码概率极低，
            // 故保留单一命名空间；若 C3 引入码空间不一致的源，需在键上区分「匹配码/原文码」来源。
            var netCode = net.Status == 1 ? net.Code : (row.F网点编码 ?? "");
            if (net.Status == 0) networkUnmatched++;

            // ── 业务日期（本源 F统计日期 为 yyyyMMdd/带分隔均可，ParseUtil.TryDate 已兼容）──
            var bizDate = ParseUtil.TryDate(row.F统计日期);
            if (bizDate == null || string.IsNullOrEmpty(netCode))
            {
                // 日期解析失败 或 无网点编码（连回退原文都空）→ 跳过（无法构成唯一键）。
                skipped++;
                continue;
            }

            // ── 合并 upsert（唯一键：FOrgId × 承运商 × 业务日期 × 网点编码）──
            // 先查同批缓存（命中=本批前面已处理过同键行，复用实例，isNew=false 避免重复 Add）；
            // 未命中再查库；库无则 new+Add+入缓存。
            var cacheKey = (bizDate.Value, netCode!);
            bool isNew = false;
            if (!metricCache.TryGetValue(cacheKey, out var metric))
            {
                var existing = await _db.Set<QlShentongNetworkDailyMetric>()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m =>
                        m.FOrgId == orgId &&
                        m.F承运商 == "申通" &&
                        m.F业务日期 == bizDate &&
                        m.F网点编码 == netCode, ct);

                isNew = existing == null;
                metric = existing ?? new QlShentongNetworkDailyMetric();
                metricCache[cacheKey] = metric;
            }

            // 键 + 公共维度（多源都可写；名称/片区/省区取本源原文，仅在有值时刷新，避免别源已填被清空）。
            metric.FOrgId = orgId;
            metric.F承运商 = "申通";
            metric.F业务日期 = bizDate.Value;
            metric.F统计年月 = ParseUtil.Ym(bizDate);
            metric.F网点编码 = netCode!;
            if (!string.IsNullOrWhiteSpace(row.F网点名称)) metric.F网点名称 = row.F网点名称;
            if (!string.IsNullOrWhiteSpace(row.F片区名称)) metric.F片区 = row.F片区名称;
            if (!string.IsNullOrWhiteSpace(row.F省区)) metric.F省区 = row.F省区;

            // ── 本源负责子集：积压与遗失（逐列以两实体真实名对齐）──
            // 积压
            metric.F日均出港量 = ParseUtil.TryInt(row.F日均出港量);
            metric.F日均进港量 = ParseUtil.TryInt(row.F日均进港量);
            metric.F积压倍数 = ParseUtil.TryDecimal(row.F积压倍数);
            metric.F超3天积压量 = ParseUtil.TryInt(row.F超3天积压量疑似遗失);
            metric.F超5天积压量 = ParseUtil.TryInt(row.F超5天积压量智能遗失);
            metric.F超7天积压量 = ParseUtil.TryInt(row.F超7天积压量超长单);
            // 遗失
            metric.F遗失率ppm = ParseUtil.TryDecimal(row.F遗失率ppm);
            metric.F遗失量 = ParseUtil.TryInt(row.F遗失量);
            // 进港投诉 / 虚签
            metric.F进港投诉量 = ParseUtil.TryInt(row.F进港投诉量);
            metric.F进港投诉率 = ParseUtil.TryDecimal(row.F进港投诉率);
            metric.F虚签投诉率 = ParseUtil.TryDecimal(row.F虚签投诉率上一周);
            metric.F7日虚签投诉量 = ParseUtil.TryInt(row.F7日虚签投诉量);

            // 关键不变量：出仓/滞留/签收/拦截/渗透等「非本源字段」一律不写——既有值保持不变（不整行覆盖）。

            metric.F来源批次ID = row.F批次ID;

            if (isNew)
            {
                metric.F创建时间 = DateTime.Now;
                _db.Set<QlShentongNetworkDailyMetric>().Add(metric);
            }

            netUpserts++;
        }

        await _db.SaveChangesAsync(ct);

        return new UnifyResult(
            EventsUpserted: 0,
            EmployeeMetricUpserts: 0,
            NetworkMetricUpserts: netUpserts,
            NetworkUnmatched: networkUnmatched,
            EmployeeUnmatched: 0);
    }

    /// <summary>
    /// 积压监控源行的内存投影（只含归一需要的列），避免读全实体碰 NULL 系统列。
    /// </summary>
    private sealed record BacklogRow(
        long FID,
        long F批次ID,
        string? F网点编码,
        string? F网点名称,
        string? F统计日期,
        string? F片区名称,
        string? F省区,
        string? F日均出港量,
        string? F日均进港量,
        string? F积压倍数,
        string? F超3天积压量疑似遗失,
        string? F超5天积压量智能遗失,
        string? F超7天积压量超长单,
        string? F遗失率ppm,
        string? F遗失量,
        string? F进港投诉量,
        string? F进港投诉率,
        string? F虚签投诉率上一周,
        string? F7日虚签投诉量);

    /// <summary>
    /// 按唯一键 (orgId,申通,域,原文) 查问题字典；缺失则创建一条默认条目并保存（让字典从数据自动长出）。
    /// 默认值：编码=前缀+短哈希、名称=原文、严重度=1、不考核、可归责到人、状态=1。
    /// 本次运行内用 cache 去重，确保同批多行同原文只建一条（幂等）。
    /// </summary>
    private async global::System.Threading.Tasks.Task<QlShentongProblemDict> GetOrCreateProblemDictAsync(
        long orgId, ShentongSourceDescriptor desc, string problemRaw,
        Dictionary<string, QlShentongProblemDict> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(problemRaw, out var cached))
            return cached;

        var dict = await _db.Set<QlShentongProblemDict>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d =>
                d.FOrgId == orgId &&
                d.F承运商 == "申通" &&
                d.F质量域 == desc.QualityDomain &&
                d.F来源问题类型原文 == problemRaw, ct);

        if (dict == null)
        {
            dict = new QlShentongProblemDict
            {
                FOrgId = orgId,
                F承运商 = "申通",
                F质量域 = desc.QualityDomain,
                F来源问题类型原文 = problemRaw,
                F问题类型编码 = $"{desc.ProblemCodePrefix}_{ShortHash(desc.QualityDomain, problemRaw)}",
                F问题类型名称 = string.IsNullOrEmpty(problemRaw) ? "(未分类)" : problemRaw,
                F默认严重度 = 1,
                F是否考核 = false,
                F是否可归责到人 = true,
                F状态 = 1,
            };
            _db.Set<QlShentongProblemDict>().Add(dict);
            // 立即保存，保证后续行/重复运行能按唯一键查到（也让多源共用字典一致）
            await _db.SaveChangesAsync(ct);
        }

        cache[problemRaw] = dict;
        return dict;
    }

    /// <summary>
    /// 物流完整性源行的内存投影（只含归一需要的列），避免读全实体碰 NULL 系统列。
    /// </summary>
    private sealed record LogisticsRow(
        long FID,
        long F批次ID,
        string? F统计日期,
        string? F运单号,
        string? F网点名称,
        string? F所属网点名称,
        string? F问题类型,
        string? F订单平台,
        string? F签收员编号,
        string? F签收员名称);

    /// <summary>关键列快照 JSON（用于审计/回溯，存事件的 F关键字段JSON）。</summary>
    private static string BuildKeySnapshot(LogisticsRow row)
    {
        var snap = new Dictionary<string, string?>
        {
            ["F统计日期"] = row.F统计日期,
            ["F运单号"] = row.F运单号,
            ["F网点名称"] = row.F网点名称,
            ["F所属网点名称"] = row.F所属网点名称,
            ["F问题类型"] = row.F问题类型,
            ["F订单平台"] = row.F订单平台,
            ["F签收员编号"] = row.F签收员编号,
            ["F签收员名称"] = row.F签收员名称,
        };
        return JsonSerializer.Serialize(snap);
    }

    /// <summary>
    /// 由「域 + 原文」生成稳定短哈希（8 位十六进制大写），保证同一原文恒得同码（幂等可复现），
    /// 不同原文极低碰撞。编码全局只需在字典唯一键内可辨识，短哈希足够。
    /// </summary>
    private static string ShortHash(string domain, string raw)
    {
        var bytes = Encoding.UTF8.GetBytes($"{domain}|{raw}");
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(8);
        for (int i = 0; i < 4; i++)
            sb.Append(hash[i].ToString("X2"));
        return sb.ToString();
    }
}
