using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.AutoPlugin.Implementations;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Import;
using STOTOP.Module.CardFlow.Services.Import.TransformEngine;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.OA.Entities;
using STOTOP.Module.Quality.Entities;
using STOTOP.Module.Quality.Services.Unification;
using STOTOP.Module.System.Entities;
using STOTOP.WebAPI.Data.Seeders;
using Xunit;
using Xunit.Abstractions;

namespace STOTOP.Module.CardFlow.Tests.Unify;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL Task 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// Plan2 D1 — 申通「导入 → 归一」全链路 e2e 收口验收（连开发测试库 stotop）。
/// 这是 Plan2 的最终验证：证明<b>多源一起归一</b>（事件 + 员工日指标 + 网点日指标 + 主数据匹配 +
/// 幂等 + 重跑回填）端到端贯通。单个 [SkippableFact]，选<b>代表性快子集</b>（避开 OutboundMonitor/
/// HomeDelivery 等大行慢源），覆盖三类归一目标 + 匹配 + 合并 + 回填即可。
///
/// 代表性子集（均小、快；网点全为 江苏太仓市城区公司/320288）：
///  事件类：
///   ① 物流完整性明细（excel (未到件).xls，15 行，2026-06-16）——签收员有真工号列 F签收员编号=3202886603，
///      但<b>不种该工号的主数据</b>（脏签收员名「城区029申亚楠1763893282」、无别名、启发式不命中）→ 首跑员工进「待认领」(0)，供 ⑥ 回填验证。
///   ② 进港投诉明细（9 行，2026-06-15）——F小件员编码=真工号；<b>种 2 个真工号的 ExpSalesman</b> → 这些行员工匹配状态=1（工号命中）。
///  员工日指标：
///   ③ 小件员履约指标（59 行）——员工脏名；<b>种 1 个 ExpSalesmanAlias</b> 让其中 1 名员工经别名命中(状态2)→ 建员工日指标行（含工号）。
///  网点日指标：
///   ④ 积压监控汇总（1 行，2026-06-15）——填积压子集（积压倍数/超N天积压量/遗失…）。
///   ⑤ 出仓考核汇总（1 行，2026-06-16）——填出仓子集（一频次出仓及时率/未及时出仓量/出仓预估考核金额）。
///      两 NM 源字段子集互不相交：⑤ 落 2026-06-16，与预置在 2026-06-16 的「积压哨兵」同 (网点×日) 行合并，
///      验证出仓归一<b>不清空</b>积压哨兵字段（合并 upsert 不抢字段）。
///
/// 断言（覆盖计划 D1 各点）：
///  ① 事件：QL申通_承运商质量事件 覆盖所选事件域（物流信息/投诉与赔付 各有事件，F问题类型名称 非空）；
///  ② 员工日指标：QL申通_员工日质量指标 有行，匹配到的员工工号非空；
///  ③ 网点日指标：网点 320288 有行，积压子集（06-15 + 06-16 哨兵）与出仓子集（06-16）各自落值、互不清空（合并）；
///  ④ 主数据匹配：网点 320288 事件/指标 F网点匹配状态=1；有工号进港投诉事件 F员工匹配状态=1；脏名无主数据物流完整性事件进「待认领」(0或3)；
///  ⑤ 幂等：再跑 UnifyShentongAsync 一次，三表本测试行数均不变；
///  ⑥ 重跑回填：给物流完整性脏签收员名补 ExpSalesmanAlias（+ ExpSalesman）→ RematchUnresolvedAsync → 该事件 F员工匹配状态 变 2、F员工工号 回填，事件总数不变。
///
/// cleanup：try/finally 删本 orgId 的 QL 数据 + 本批 STG 行 + 残留 + 种子（别名/业务员/网点）。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class ShentongUnifyBatchE2ETests
{
    private const long OrgId = 192;
    private const string NetCode = "320288";
    private const string NetFullName = "江苏太仓市城区公司";

    private const string EventTable = "QL申通_承运商质量事件";
    private const string DictTable = "QL申通_质量问题字典";
    private const string EmpMetricTable = "QL申通_员工日质量指标";
    private const string NetMetricTable = "QL申通_网点日质量指标";

    // 事件/指标的真实业务日期（来自样例文件实读）
    private static readonly DateTime DateLogi = new(2026, 6, 16);     // 物流完整性事件
    private static readonly DateTime DateInbound = new(2026, 6, 15);  // 进港投诉事件
    private static readonly DateTime DateBacklog = new(2026, 6, 15);  // 积压监控网点日指标
    private static readonly DateTime DateOutbound = new(2026, 6, 16); // 出仓考核网点日指标（与积压哨兵同日合并）

    // ── 进港投诉真工号（样例实读）：种这 2 个的 ExpSalesman → 其事件员工匹配状态=1 ──
    private const string InboundEmpNo1 = "3202885246"; // 城区张闯华（1 行）
    private const string InboundEmpNo2 = "3202880036"; // 城区屈梦幻400（2 行）

    // ── 物流完整性脏签收员名（样例实读，15 行同此名）：首跑待认领，⑥ 用别名回填 ──
    private const string LogiDirtyName = "城区029申亚楠17638932823";
    private const string AliasEmpNo = "ETEST_ALIAS_001"; // 回填用测试工号（库中不存在，不与真工号冲突）

    // ── 小件员履约：取样例真实脏名之一，补别名让该员工经别名命中(状态2)→ 建员工日指标行 ──
    private const string CourierDirtyName = "城区吴健304"; // 履约样例首数据行小件员
    private const string CourierEmpNo = "ETEST_COURIER_001";

    private static readonly string 明细Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细";
    private static readonly string 展示页Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据展示页";

    // 样例文件
    private const string LogiFile = "excel (未到件).xls";
    private const string InboundFile = "进港投诉明细_20260617_63ed1558c5af431fa87fd9e485df65a0.xlsx";
    private const string CourierFile = "小件员履约指标历史数据导出_导出任务_20260617_109361182.xlsx";
    private const string BacklogFile = "积压异常监控_导出任务_20260617_109361094.xlsx";
    private const string OutboundFile = "出仓考核汇总导出_导出任务_20260617_109361069.xlsx";

    private readonly ITestOutputHelper _log;
    public ShentongUnifyBatchE2ETests(ITestOutputHelper log) => _log = log;

    [SkippableFact]
    public async Task Unify_ImportToUnify_E2E_Events_EmpMetric_NetMetricMerge_Match_Idempotent_Rematch()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        var logiPath = global::System.IO.Path.Combine(明细Dir, LogiFile);
        var inboundPath = global::System.IO.Path.Combine(明细Dir, InboundFile);
        var courierPath = global::System.IO.Path.Combine(展示页Dir, CourierFile);
        var backlogPath = global::System.IO.Path.Combine(展示页Dir, BacklogFile);
        var outboundPath = global::System.IO.Path.Combine(展示页Dir, OutboundFile);
        Skip.IfNot(File.Exists(logiPath), $"样例文件不存在: {logiPath}");
        Skip.IfNot(File.Exists(inboundPath), $"样例文件不存在: {inboundPath}");
        Skip.IfNot(File.Exists(courierPath), $"样例文件不存在: {courierPath}");
        Skip.IfNot(File.Exists(backlogPath), $"样例文件不存在: {backlogPath}");
        Skip.IfNot(File.Exists(outboundPath), $"样例文件不存在: {outboundPath}");

        await using var db = CreateDbContext(conn);
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }

        CardFlowSeeder.Migrate(db);
        QualityUnifySeeder.EnsureTables(db);

        var stg = new List<(long id, string table)>();
        var seeded = new SeedFlags();
        try
        {
            // ── arrange：预清本 org QL + 本测试源 STG 残留（按 org 全域，自愈机制就位）──
            await CleanupQualityAsync(conn);
            await ShentongStgResidueReset.CleanAsync(conn);

            // 种网点 + 进港投诉 2 工号 + 回填/履约别名用业务员（别名先不种，⑥ 才补）
            await SeedMasterDataAsync(db, seeded);

            // ── 导入代表性子集（覆盖三类目标 + 匹配 + 合并 + 回填）──
            await ImportAndExpect(db, conn, stg, logiPath, 2301, 5101, 3101, ShentongSourceMap.LogisticsCompletenessTable, 15);
            await ImportAndExpect(db, conn, stg, inboundPath, 2310, 5110, 3110, ShentongSourceMap.InboundComplaintTable, 9);
            await ImportAndExpect(db, conn, stg, courierPath, 2324, 5124, 3124, ShentongSourceMap.CourierFulfillTable, 59);
            await ImportAndExpect(db, conn, stg, backlogPath, 2326, 5126, 3126, ShentongSourceMap.BacklogMonitorTable, 1);
            await ImportAndExpect(db, conn, stg, outboundPath, 2323, 5123, 3123, ShentongSourceMap.OutboundAssessTable, 1);

            // ── 网点日指标合并哨兵：预置一条 (320288, 2026-06-16) 行，手填积压子集 F超3天积压量=999 ──
            // 出仓考核样例落 2026-06-16 → 归一时只刷出仓子集，绝不能清掉积压哨兵（合并 upsert 不抢字段）。
            await ExecAsync(conn,
                $@"INSERT INTO [{NetMetricTable}] ([FOrgId],[F承运商],[F业务日期],[F网点编码],[F超3天积压量],[F创建时间])
                   VALUES (@org, N'申通', @d, @code, 999, GETDATE())",
                ("@org", OrgId), ("@d", DateOutbound), ("@code", NetCode));

            // ── act：一次归一所有源 ──
            var svc = new QualityUnificationService(db, new MasterDataMatcher(db));
            var r1 = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[首跑] Events={r1.EventsUpserted} Emp={r1.EmployeeMetricUpserts} Net={r1.NetworkMetricUpserts} " +
                           $"NetUnmatched={r1.NetworkUnmatched} EmpUnmatched={r1.EmployeeUnmatched}");

            // ════════════════════════════════════════════════════════════
            // 断言 ① 事件：覆盖物流信息（物流完整性）+ 投诉与赔付（进港投诉）两域，F问题类型名称 非空
            // ════════════════════════════════════════════════════════════
            var logiEvents = await EventCountAsync(conn, ShentongSourceMap.LogisticsCompletenessTable);
            var inboundEvents = await EventCountAsync(conn, ShentongSourceMap.InboundComplaintTable);
            _log.WriteLine($"[① 事件] 物流完整性事件={logiEvents}（期望15） 进港投诉事件={inboundEvents}（期望9）");
            Assert.Equal(15, logiEvents);
            Assert.Equal(9, inboundEvents);

            // 域正确：物流完整性=物流信息域；进港投诉=投诉与赔付域
            Assert.Equal(0, await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@t AND [F质量域]<>N'物流信息'",
                ("@org", OrgId), ("@t", ShentongSourceMap.LogisticsCompletenessTable)));
            Assert.Equal(0, await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@t AND [F质量域]<>N'投诉与赔付'",
                ("@org", OrgId), ("@t", ShentongSourceMap.InboundComplaintTable)));

            // F问题类型名称 全非空（含空单元格兜底「(未分类)」也非空）
            var emptyProblem = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org
                   AND [F来源STG表] IN (@t1,@t2)
                   AND ([F问题类型名称] IS NULL OR LTRIM(RTRIM([F问题类型名称]))=N'')",
                ("@org", OrgId), ("@t1", ShentongSourceMap.LogisticsCompletenessTable), ("@t2", ShentongSourceMap.InboundComplaintTable));
            Assert.Equal(0, emptyProblem);

            // ════════════════════════════════════════════════════════════
            // 断言 ② 员工日指标：小件员履约经别名命中的员工 → 有行，工号非空
            // ════════════════════════════════════════════════════════════
            var empMetricRows = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EmpMetricTable}] WHERE [FOrgId]=@org AND [F员工工号]=@no",
                ("@org", OrgId), ("@no", CourierEmpNo));
            _log.WriteLine($"[② 员工日指标] 别名命中员工({CourierEmpNo}) 指标行数={empMetricRows}（期望1）");
            Assert.Equal(1, empMetricRows);
            // 该行工号/网点非空且为种子值
            var empNoNotEmpty = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{EmpMetricTable}] WHERE [FOrgId]=@org AND [F员工工号]=@no
                   AND [F网点编码]=@code AND [F员工工号] IS NOT NULL AND LTRIM(RTRIM([F员工工号]))<>N''",
                ("@org", OrgId), ("@no", CourierEmpNo), ("@code", NetCode));
            Assert.Equal(1, empNoNotEmpty);

            // ════════════════════════════════════════════════════════════
            // 断言 ③ 网点日指标合并：积压子集 / 出仓子集 在 (网点×日) 行各自落值、互不清空
            // ════════════════════════════════════════════════════════════
            // 积压 06-15 行：积压子集落值（积压倍数 / 超3天积压量 等）。
            await AssertNetNotNull(conn, DateBacklog, "F积压倍数");
            await AssertNetNotNull(conn, DateBacklog, "F超3天积压量");
            // 出仓 06-16 行：出仓子集落值（一频次出仓及时率 / 未及时出仓量 / 出仓预估考核金额）。
            await AssertNetNotNull(conn, DateOutbound, "F一频次出仓及时率");
            await AssertNetNotNull(conn, DateOutbound, "F未及时出仓量");
            await AssertNetNotNull(conn, DateOutbound, "F出仓预估考核金额");

            // 合并不抢字段：06-16 行 既被出仓考核填上 F一频次出仓及时率，又保留预置的积压哨兵 F超3天积压量=999。
            var noSteal16 = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{NetMetricTable}]
                   WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code
                     AND [F一频次出仓及时率] IS NOT NULL AND [F超3天积压量]=999",
                ("@org", OrgId), ("@d", DateOutbound), ("@code", NetCode));
            _log.WriteLine($"[③ 合并不抢字段] 06-16 行 出仓子集 + 积压哨兵999 共存 行数={noSteal16}（期望1）");
            Assert.Equal(1, noSteal16);

            // 子集互不相交：06-16 行不应有出仓源之外的「真实积压数据」抢入（积压源在 06-15）；
            // 06-15 积压行不应被出仓子集污染（F一频次出仓及时率 应为 NULL）。
            var outboundClean15 = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{NetMetricTable}]
                   WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code
                     AND [F积压倍数] IS NOT NULL AND [F一频次出仓及时率] IS NULL",
                ("@org", OrgId), ("@d", DateBacklog), ("@code", NetCode));
            _log.WriteLine($"[③ 子集不相交] 06-15 积压行 有积压倍数 且 无出仓及时率 行数={outboundClean15}（期望1）");
            Assert.Equal(1, outboundClean15);

            // 网点日指标行数：恰 2 行（06-15 积压 + 06-16 出仓/哨兵合并）。
            var netRows = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{NetMetricTable}] WHERE [FOrgId]=@org AND [F网点编码]=@code",
                ("@org", OrgId), ("@code", NetCode));
            _log.WriteLine($"[③ 网点日指标行数] (网点×日)={netRows}（期望2：06-15/06-16）");
            Assert.Equal(2, netRows);

            // ════════════════════════════════════════════════════════════
            // 断言 ④ 主数据匹配
            // ════════════════════════════════════════════════════════════
            // 网点匹配：两事件源 + 网点日指标 网点 320288 全部 F网点匹配状态=1。
            var netUnmatchedEvt = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表] IN (@t1,@t2)
                   AND ([F网点编码]<>@code OR [F网点匹配状态]<>1)",
                ("@org", OrgId), ("@t1", ShentongSourceMap.LogisticsCompletenessTable),
                ("@t2", ShentongSourceMap.InboundComplaintTable), ("@code", NetCode));
            _log.WriteLine($"[④ 网点匹配] 事件网点非(320288/状态1) 行数={netUnmatchedEvt}（期望0）");
            Assert.Equal(0, netUnmatchedEvt);
            var netMetricUnmatched = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{NetMetricTable}] WHERE [FOrgId]=@org AND [F网点编码]=@code",
                ("@org", OrgId), ("@code", NetCode));
            Assert.True(netMetricUnmatched >= 2, "网点日指标应以 320288 编码落行（网点匹配命中码）");

            // 有工号进港投诉事件：种了的 2 工号对应事件 F员工匹配状态=1、工号非空。
            var inboundMatched = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@t
                   AND [F员工工号] IN (@n1,@n2) AND [F员工匹配状态]=1",
                ("@org", OrgId), ("@t", ShentongSourceMap.InboundComplaintTable),
                ("@n1", InboundEmpNo1), ("@n2", InboundEmpNo2));
            _log.WriteLine($"[④ 员工工号命中] 进港投诉 种子工号事件 状态1 行数={inboundMatched}（期望≥3：工号{InboundEmpNo1}×1 + {InboundEmpNo2}×2）");
            Assert.True(inboundMatched >= 3, $"种子工号事件应全部状态1，实际 {inboundMatched}");

            // 脏名无主数据物流完整性事件：进「待认领」(0 或 3)、工号为空（首跑未补别名）。
            var logiPending = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@t
                   AND [F员工匹配状态] IN (0,3) AND [F员工工号] IS NULL",
                ("@org", OrgId), ("@t", ShentongSourceMap.LogisticsCompletenessTable));
            _log.WriteLine($"[④ 待认领] 物流完整性脏名事件 状态0/3且工号空 行数={logiPending}（期望15）");
            Assert.Equal(15, logiPending);

            // ════════════════════════════════════════════════════════════
            // 断言 ⑤ 幂等：再跑一次 → 三表本测试行数均不变
            // ════════════════════════════════════════════════════════════
            var evtBefore = await CountAsync(conn, $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
            var empBefore = await CountAsync(conn, $"SELECT COUNT(*) FROM [{EmpMetricTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
            var netBefore = await CountAsync(conn, $"SELECT COUNT(*) FROM [{NetMetricTable}] WHERE [FOrgId]=@org", ("@org", OrgId));

            var r2 = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[复跑] Events={r2.EventsUpserted} Emp={r2.EmployeeMetricUpserts} Net={r2.NetworkMetricUpserts}");

            var evtAfter = await CountAsync(conn, $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
            var empAfter = await CountAsync(conn, $"SELECT COUNT(*) FROM [{EmpMetricTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
            var netAfter = await CountAsync(conn, $"SELECT COUNT(*) FROM [{NetMetricTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
            _log.WriteLine($"[⑤ 幂等] 事件 {evtBefore}->{evtAfter} 员工 {empBefore}->{empAfter} 网点 {netBefore}->{netAfter}");
            Assert.Equal(evtBefore, evtAfter);
            Assert.Equal(empBefore, empAfter);
            Assert.Equal(netBefore, netAfter);
            // 哨兵仍在（复跑后 06-16 行 出仓子集 + 积压哨兵 共存）。
            Assert.Equal(1, await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{NetMetricTable}]
                   WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code
                     AND [F一频次出仓及时率] IS NOT NULL AND [F超3天积压量]=999",
                ("@org", OrgId), ("@d", DateOutbound), ("@code", NetCode)));

            // ════════════════════════════════════════════════════════════
            // 断言 ⑥ 重跑回填：补别名 → RematchUnresolvedAsync → 物流完整性脏名事件 状态变2、工号回填
            // ════════════════════════════════════════════════════════════
            // 补：测试业务员（工号 ETEST_ALIAS_001，网点 320288）+ 别名（脏名 → 该工号）。
            await SeedAliasForRematchAsync(db, seeded);

            var rematch = await svc.RematchUnresolvedAsync(OrgId);
            _log.WriteLine($"[⑥ 回填] NetworkRebound={rematch.NetworkRebound} EmployeeRebound={rematch.EmployeeRebound} Scanned={rematch.Scanned}");

            // 物流完整性 15 行脏名事件 → 现别名命中(状态2)、工号回填 AliasEmpNo。
            var rebound = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@t
                   AND [F员工匹配状态]=2 AND [F员工工号]=@no",
                ("@org", OrgId), ("@t", ShentongSourceMap.LogisticsCompletenessTable), ("@no", AliasEmpNo));
            _log.WriteLine($"[⑥ 回填] 物流完整性事件 状态2+工号{AliasEmpNo} 行数={rebound}（期望15）");
            Assert.Equal(15, rebound);

            // 事件总数不变（回填只改主数据列，不增/删事件）。
            var evtAfterRematch = await CountAsync(conn, $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
            _log.WriteLine($"[⑥ 回填] 回填后事件总数={evtAfterRematch}（期望 {evtAfter} 不变）");
            Assert.Equal(evtAfter, evtAfterRematch);

            // 回填幂等：再回填一次无变化（别名已绑定，无候选）。
            var rematch2 = await svc.RematchUnresolvedAsync(OrgId);
            _log.WriteLine($"[⑥ 回填幂等] 二次 EmployeeRebound={rematch2.EmployeeRebound}（期望0）");
            Assert.Equal(0, rematch2.EmployeeRebound);
        }
        finally
        {
            await CleanupQualityAsync(conn);
            foreach (var (id, table) in stg) await CleanupBatchAsync(conn, table, id);
            await ShentongStgResidueReset.CleanAsync(conn);
            await CleanupSeedAsync(conn, seeded);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 断言 helper
    // ─────────────────────────────────────────────────────────────

    private async Task AssertNetNotNull(string conn, DateTime date, string col)
    {
        var n = await CountAsync(conn,
            $"SELECT COUNT(*) FROM [{NetMetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code AND [{col}] IS NOT NULL",
            ("@org", OrgId), ("@d", date), ("@code", NetCode));
        _log.WriteLine($"[网点子集非空 {date:MM-dd}] [{col}] 非空行数={n}（期望1）");
        Assert.Equal(1, n);
    }

    private static Task<int> EventCountAsync(string conn, string stgTable) =>
        CountAsync(conn,
            $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@t",
            ("@org", OrgId), ("@t", stgTable));

    // ─────────────────────────────────────────────────────────────
    // 种子 / 导入 / 清理 helper
    // ─────────────────────────────────────────────────────────────

    private sealed class SeedFlags
    {
        public bool Network;
        public bool InboundEmp1;
        public bool InboundEmp2;
        public bool AliasSalesman;     // 回填用业务员
        public bool AliasRow;          // 回填用别名
        public bool CourierSalesman;   // 履约别名命中用业务员
        public bool CourierAliasRow;   // 履约别名
    }

    /// <summary>种网点 320288 + 进港投诉 2 真工号业务员 + 履约别名命中业务员/别名（履约别名首跑即生效，验证员工日指标）。</summary>
    private async Task SeedMasterDataAsync(STOTOPDbContext db, SeedFlags seeded)
    {
        // 网点（编码 320288 + 全称）——320288 是真实网点编码，保守用「不存在才种」+ seeded 门控清理，避免误删真实主数据。
        if (!await db.Set<ExpNetworkPoint>().IgnoreQueryFilters().AnyAsync(np => np.FCode == NetCode && np.FOrgId == OrgId))
        {
            db.Set<ExpNetworkPoint>().Add(new ExpNetworkPoint
            {
                FCode = NetCode, FFullName = NetFullName, FOrgId = OrgId, FOwnerOrgId = OrgId,
            });
            seeded.Network = true;
        }

        // 进港投诉真工号业务员（工号命中 → 状态1）——真实工号，保守用「不存在才种」+ seeded 门控清理，绝不 force-correct/无条件删（会误删真实主数据）。
        seeded.InboundEmp1 = await EnsureSalesmanAsync(db, InboundEmpNo1, "张闯华");
        seeded.InboundEmp2 = await EnsureSalesmanAsync(db, InboundEmpNo2, "屈梦幻");

        // 小件员履约：合成工号业务员 + 脏名别名（首跑即让该 1 名员工经别名命中状态2 → 建员工日指标行）。
        // 合成工号 force-correct（删任何既有再插）；脏名别名 force-correct（删任何 (脏名+org) 既有再插，使本测试映射权威，
        // 不被残留/顺序污染——根治「城区吴健304 残留映射到别的工号」跨类隔离假阴性）。
        await ForceCorrectSalesmanAsync(db, CourierEmpNo, "吴健");
        seeded.CourierSalesman = true;
        await ForceCorrectAliasAsync(db, CourierDirtyName, CourierEmpNo);
        seeded.CourierAliasRow = true;

        await db.SaveChangesAsync();
    }

    /// <summary>⑥ 回填：补物流完整性脏签收员名 → 测试工号的别名 + 该工号业务员（首跑后才补，制造「补别名→重跑回填」）。</summary>
    private async Task SeedAliasForRematchAsync(STOTOPDbContext db, SeedFlags seeded)
    {
        // 合成工号 + 脏名别名，同样 force-correct（合成工号/脏名均测试专属，删任何既有再插，范围安全）。
        await ForceCorrectSalesmanAsync(db, AliasEmpNo, "申亚楠");
        seeded.AliasSalesman = true;
        await ForceCorrectAliasAsync(db, LogiDirtyName, AliasEmpNo);
        seeded.AliasRow = true;
        await db.SaveChangesAsync();
    }

    /// <summary>种业务员（工号主键，关联网点 320288）。返回是否本测试新种（供清理）。仅用于<b>真实工号</b>（不存在才种，避免误删真实主数据）。</summary>
    private static async Task<bool> EnsureSalesmanAsync(STOTOPDbContext db, string empNo, string name)
    {
        if (await db.Set<ExpSalesman>().IgnoreQueryFilters().AnyAsync(s => s.FEmployeeNo == empNo))
            return false;
        db.Set<ExpSalesman>().Add(new ExpSalesman
        {
            FEmployeeNo = empNo,
            FNetworkPointCode = NetCode,
            FEmployeeId = 0,
            FName = name,
            FStatus = 1,
        });
        return true;
    }

    /// <summary>force-correct 合成工号业务员（删任何既有同工号再插）。仅用于<b>测试合成工号</b>（ETEST_*，库中不存在/不与真工号冲突）。</summary>
    private static async Task ForceCorrectSalesmanAsync(STOTOPDbContext db, string empNo, string name)
    {
        var existing = await db.Set<ExpSalesman>().IgnoreQueryFilters().Where(s => s.FEmployeeNo == empNo).ToListAsync();
        if (existing.Count > 0) db.Set<ExpSalesman>().RemoveRange(existing);
        db.Set<ExpSalesman>().Add(new ExpSalesman
        {
            FEmployeeNo = empNo,
            FNetworkPointCode = NetCode,
            FEmployeeId = 0,
            FName = name,
            FStatus = 1,
        });
    }

    /// <summary>force-correct 脏名别名（删任何既有 (脏名+org) 再插本测试期望映射）。使本测试对其脏名→工号映射权威，不受残留/顺序影响。</summary>
    private static async Task ForceCorrectAliasAsync(STOTOPDbContext db, string dirtyName, string empNo)
    {
        var existing = await db.Set<ExpSalesmanAlias>().IgnoreQueryFilters().Where(a => a.FName == dirtyName && a.FOrgId == OrgId).ToListAsync();
        if (existing.Count > 0) db.Set<ExpSalesmanAlias>().RemoveRange(existing);
        db.Set<ExpSalesmanAlias>().Add(new ExpSalesmanAlias { FName = dirtyName, FEmployeeNo = empNo, FOrgId = OrgId });
    }

    private async Task ImportAndExpect(
        STOTOPDbContext db, string conn, List<(long, string)> stg,
        string path, long flow, long stage, long rule, string table, int expected)
    {
        var (id, result) = await ImportOnceAsync(db, conn, path, flow, stage, rule);
        stg.Add((id, table));
        Assert.True(result.Success, $"[{table}] 导入应成功，实际 Message={result.Message}");
        var rows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{table}] WHERE [F批次ID]=@b", ("@b", id));
        _log.WriteLine($"[导入 {table}] 期望 {expected} 行，实际 {rows} 行 (batch={id})");
        Assert.Equal(expected, rows);
    }

    private async Task<(long batchId, PluginResult result)> ImportOnceAsync(
        STOTOPDbContext db, string conn, string filePath, long flowDefinitionId, long stageId, long ruleId)
    {
        var localPath = Snapshot(filePath);
        var batch = new CfBatch
        {
            FFlowDefinitionId = flowDefinitionId,
            FOrgId = OrgId,
            FTriggeredById = 1,
            FTriggeredTime = DateTime.Now,
            FTriggerType = "fileUpload",
            FFilePath = localPath,
            FFileName = global::System.IO.Path.GetFileName(filePath),
            FBatchNo = $"TESTD1-{Guid.NewGuid():N}",
            FStatus = CfBatchStatus.Parsing,
            FUploadMethod = "auto",
            FCreatedTime = DateTime.Now,
        };
        db.Set<CfBatch>().Add(batch);
        await db.SaveChangesAsync();
        var batchId = batch.FID;

        var config = new ConfigurationBuilder().Build();
        var plugin = new ExcelInputPlugin(
            new ExcelParserService(),
            new EfCoreBulkInsertService(db),
            new JintTransformEngine(NullLogger<JintTransformEngine>.Instance),
            new NoopProgressReporter(),
            db,
            NullLogger<ExcelInputPlugin>.Instance,
            config);

        var ctx = new PluginContext
        {
            BatchId = batchId,
            StageDefinitionId = stageId,
            PluginRuleId = ruleId,
            OrgId = OrgId,
            Services = null!,
        };

        var result = await plugin.ExecuteAsync(ctx);
        return (batchId, result);
    }

    private static async Task CleanupQualityAsync(string conn)
    {
        await ExecAsync(conn, $"DELETE FROM [{EventTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
        await ExecAsync(conn, $"DELETE FROM [{DictTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
        await ExecAsync(conn, $"DELETE FROM [{EmpMetricTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
        await ExecAsync(conn, $"DELETE FROM [{NetMetricTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
    }

    private async Task CleanupBatchAsync(string conn, string table, long batchId)
    {
        if (batchId <= 0) return;
        try { await ExecAsync(conn, $"DELETE FROM [{table}] WHERE [F批次ID]=@b", ("@b", batchId)); }
        catch { /* ignore */ }
        try
        {
            await using var cleanup = CreateDbContext(conn);
            var b = await cleanup.Set<CfBatch>().FirstOrDefaultAsync(x => x.FID == batchId);
            if (b != null) { cleanup.Set<CfBatch>().Remove(b); await cleanup.SaveChangesAsync(); }
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// 删本测试种子。脏名别名 + 合成工号（ETEST_*）<b>无条件删</b>本测试用到的具体值（arrange 已 force-correct 由本测试写入，
    /// 删除范围安全，不再用 seeded 门控——否则上轮残留致 seeded=false 时跳过清理，反复污染别的类）。
    /// 真实工号业务员（进港投诉）+ 真实网点编码 仍用 seeded 门控，仅删本测试新种的，绝不误删真实主数据。
    /// </summary>
    private async Task CleanupSeedAsync(string conn, SeedFlags seeded)
    {
        // 脏名别名（测试专属脏名）：无条件删。
        await ExecAsync(conn, "DELETE FROM [EXP快递业务员名称映射] WHERE [F名称]=@n AND [F组织ID]=@org", ("@n", LogiDirtyName), ("@org", OrgId));
        await ExecAsync(conn, "DELETE FROM [EXP快递业务员名称映射] WHERE [F名称]=@n AND [F组织ID]=@org", ("@n", CourierDirtyName), ("@org", OrgId));
        // 合成工号业务员（ETEST_*，库中不存在/不与真工号冲突）：无条件删。
        await DeleteSalesmanAsync(conn, AliasEmpNo);
        await DeleteSalesmanAsync(conn, CourierEmpNo);
        // 真实工号业务员：仅删本测试新种的（不存在才种过），避免误删真实主数据。
        if (seeded.InboundEmp1) await DeleteSalesmanAsync(conn, InboundEmpNo1);
        if (seeded.InboundEmp2) await DeleteSalesmanAsync(conn, InboundEmpNo2);
        // 网点（真实编码 320288）：仅删本测试新种的。
        if (seeded.Network)
            await ExecAsync(conn, "DELETE FROM [EXP快递网点] WHERE [F编号]=@code AND [F组织ID]=@org", ("@code", NetCode), ("@org", OrgId));
    }

    private static async Task DeleteSalesmanAsync(string conn, string empNo)
    {
        try { await ExecAsync(conn, "DELETE FROM [EXP业务员] WHERE [F工号]=@no", ("@no", empNo)); }
        catch { /* ignore */ }
    }

    // ─────────────────────────────────────────────────────────────
    // 基础设施
    // ─────────────────────────────────────────────────────────────

    private static void RegisterModules()
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(CfCard).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(OaExpenseRequest).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(SysUser).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(FinVoucher).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(ExpSalesman).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(QlException).Assembly);
    }

    private static STOTOPDbContext CreateDbContext(string conn)
    {
        RegisterModules();
        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseSqlServer(conn)
            .Options;
        return new STOTOPDbContext(options);
    }

    private sealed class NoopProgressReporter : IPluginProgressReporter
    {
        public Task ReportProgressAsync(long batchId, int processedRows, int totalRows, string? currentStep = null) => Task.CompletedTask;
        public Task ReportErrorAsync(long batchId, int rowIndex, string errorMessage) => Task.CompletedTask;
        public Task ReportCompletedAsync(long batchId, bool success, string? message = null) => Task.CompletedTask;
    }

    /// <summary>快照样例文件到临时目录（源文件可能被 Excel 打开，插件 FileShare.Read 会被独占锁拒绝）。</summary>
    private static string Snapshot(string srcPath)
    {
        var dir = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "stqc_unify_e2e_snap");
        Directory.CreateDirectory(dir);
        var dst = global::System.IO.Path.Combine(dir, global::System.IO.Path.GetFileName(srcPath));
        using (var src = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var outFs = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            src.CopyTo(outFs);
        }
        return dst;
    }

    private static async Task ExecAsync(string conn, string sql, params (string name, object val)[] ps)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, val) in ps) cmd.Parameters.AddWithValue(name, val);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(string conn, string sql, params (string name, object val)[] ps)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, val) in ps) cmd.Parameters.AddWithValue(name, val);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
