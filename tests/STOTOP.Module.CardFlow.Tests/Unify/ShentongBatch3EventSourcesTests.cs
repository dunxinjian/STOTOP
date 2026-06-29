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
/// Plan2 Phase C — C3 批次3：6 个「通用事件源」（最后一批事件）归一集成测试（连开发测试库 stotop）。
/// 6 源：投诉账单 / 虚签投诉 / 虚假签收 / 照片质检 / 履约率 / 送货上门，全走通用事件路径
/// <see cref="QualityUnificationService.UnifyShentongAsync"/> →（内部）UnifyGenericEventAsync，
/// 仅靠 <see cref="ShentongSourceMap.All"/> 里新加的描述符驱动，无服务主干改动。
///
/// 与批次1/2 同范式，本批覆盖三个新边角（逐源在 <see cref="SourceCase"/> 用开关声明）：
///   - <b>无员工维度源</b>（投诉账单）：两员工列设 null → 通用路径自动置 F员工匹配状态=9（不适用）。
///     本测专门断言：该源全部事件 F员工匹配状态=9，且 F员工工号/F员工ID/F员工姓名原文 全为空。
///   - <b>金额源</b>（投诉账单 F金额、虚签投诉 F预估考核金额）：AmountColumn → F考核金额。断言全非空、与源一致、至少 1 行 > 0。
///   - <b>过滤源</b>（照片质检 F是否质检合格=否、送货上门 F履约情况=履约失败）：事件数==满足过滤条件 STG 行数（严格少于全量）。
///   - <b>网点不命中本地种子</b>（投诉账单按受款方=外部网点、履约率系统列 F归属网点编号 抽样全空）：AssertNetworkMatch=false，不断言网点匹配。
///
/// 每源一个 [SkippableFact]（失败隔离），统一 Arrange/Act/Assert（驱动同批次2）：
///  Arrange：建 STG+QL 表；清本 org 既有 QL 事件/字典；种子 ExpNetworkPoint(320288=江苏太仓市城区公司,org=192)；导样例到对应 STG(org=192)。
///  Act：    svc.UnifyShentongAsync(192)（本源走通用路径；其它源各自处理互不干扰）。
///  Assert： 本源本批事件行数 > 0；过滤源→事件数==满足过滤行数 / 整源→==全 STG 行数；F质量域 正确；
///           问题类型名称 == STG distinct(列)（空→「(未分类)」）；网点匹配（仅 AssertNetworkMatch 源）；
///           F关键字段JSON 全非空；来源行非孤儿；【无员工源】状态9+员工三字段全空；【金额源】F考核金额 落值；再跑一次行数不变（幂等）。
///  Cleanup：try/finally 删本 org QL 事件/字典 + 本批 STG + CfBatch + 种子网点。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class ShentongBatch3EventSourcesTests
{
    private const long OrgId = 192;
    private const string NetCode = "320288";
    private const string NetFullName = "江苏太仓市城区公司";
    private const string EventTable = "QL申通_承运商质量事件";
    private const string DictTable = "QL申通_质量问题字典";

    private static readonly string 明细Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细";

    private readonly ITestOutputHelper _log;
    public ShentongBatch3EventSourcesTests(ITestOutputHelper log) => _log = log;

    // ─────────────────────────────────────────────────────────────
    // 6 个 [SkippableFact]
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ① 投诉账单明细：投诉与赔付，整源入，问题类型用列 F账单二级类型。
    /// <b>无员工维度</b>（状态9 + 员工三字段全空）；<b>带金额列 F金额 → F考核金额</b>；
    /// 网点按受款方（外部网点，不命中本地种子）→ AssertNetworkMatch=false。
    /// </summary>
    [SkippableFact]
    public Task ComplaintBill_Unify_WholeSource_NoEmployee_Status9_Amount_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "投诉账单明细",
            StgTable: ShentongSourceMap.ComplaintBillTable,
            SampleFile: "收到的投诉账单_账单明细_20260617_1781686058721.xlsx",
            Flow: 2311, Stage: 5111, Rule: 3111,
            Domain: "投诉与赔付",
            FilterColumn: null, FilterValue: null,
            ProblemTypeColumn: "F账单二级类型",
            AmountColumn: "F金额", ExpectAmount: true,
            AssertNetworkMatch: false,
            AssertEmployeeStatus9: true));

    /// <summary>
    /// ② 虚签投诉明细：虚假签收履约，整源入，问题类型用列 F投诉类型。
    /// <b>带金额列 F预估考核金额 → F考核金额</b>；网点=被投诉方(320288，命中种子)。
    /// </summary>
    [SkippableFact]
    public Task FakeSignComplaint_Unify_WholeSource_ColumnProblem_Amount_MatchesNetwork_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "虚签投诉明细",
            StgTable: ShentongSourceMap.FakeSignComplaintTable,
            SampleFile: "虚签投诉明细_20260617_db3d06b46d1147d8b0f36677dcea442d.xlsx",
            Flow: 2312, Stage: 5112, Rule: 3112,
            Domain: "虚假签收履约",
            FilterColumn: null, FilterValue: null,
            ProblemTypeColumn: "F投诉类型",
            AmountColumn: "F预估考核金额", ExpectAmount: true,
            AssertNetworkMatch: true,
            AssertEmployeeStatus9: false));

    /// <summary>
    /// ③ 虚假签收明细：虚假签收履约，整源入，问题类型用列 F投诉类型；网点=被投诉方(320288)。无金额列。源后缀 .xls 实为 xlsx。
    /// </summary>
    [SkippableFact]
    public Task FakeSign_Unify_WholeSource_ColumnProblem_MatchesNetwork_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "虚假签收明细",
            StgTable: ShentongSourceMap.FakeSignTable,
            SampleFile: "虚假签收明细导出_20260617161317_02d16fd4c12c46438adaff73d957a64b.xls",
            Flow: 2313, Stage: 5113, Rule: 3113,
            Domain: "虚假签收履约",
            FilterColumn: null, FilterValue: null,
            ProblemTypeColumn: "F投诉类型",
            AmountColumn: null, ExpectAmount: false,
            AssertNetworkMatch: true,
            AssertEmployeeStatus9: false));

    /// <summary>
    /// ④ 照片质检明细：虚假签收履约，<b>过滤 F是否质检合格=否</b>（仅不合格＝被考核），问题类型用列 F不合格类型；
    /// 网点=F网点名称(过滤后子集全为太仓，命中种子)。源后缀 .xls 实为 xlsx。
    /// </summary>
    [SkippableFact]
    public Task PhotoQc_Unify_FilteredFailed_ColumnProblem_MatchesNetwork_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "照片质检明细",
            StgTable: ShentongSourceMap.PhotoQcTable,
            SampleFile: "b20606c963a5471eaf1e443c1aa42564抖音照片质检.xls",
            Flow: 2314, Stage: 5114, Rule: 3114,
            Domain: "虚假签收履约",
            FilterColumn: "F是否质检合格", FilterValue: "否",
            ProblemTypeColumn: "F不合格类型",
            AmountColumn: null, ExpectAmount: false,
            AssertNetworkMatch: true,
            AssertEmployeeStatus9: false));

    /// <summary>
    /// ⑤ 履约率明细：虚假签收履约，整源入（抽样全「履约失败」，无正常行→不过滤），问题类型用列 F履约状态；
    /// <b>无可用网点列</b>（系统列 F归属网点编号 抽样全空）→ AssertNetworkMatch=false。
    /// </summary>
    [SkippableFact]
    public Task FulfillRate_Unify_WholeSource_ColumnProblem_NoNetwork_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "履约率明细",
            StgTable: ShentongSourceMap.FulfillRateTable,
            SampleFile: "客户声音履约率_20260617_60d02117a1384c3392c2e6d488c36339.xlsx",
            Flow: 2315, Stage: 5115, Rule: 3115,
            Domain: "虚假签收履约",
            FilterColumn: null, FilterValue: null,
            ProblemTypeColumn: "F履约状态",
            AmountColumn: null, ExpectAmount: false,
            AssertNetworkMatch: false,
            AssertEmployeeStatus9: false));

    /// <summary>
    /// ⑥ 送货上门明细：虚假签收履约，<b>过滤 F履约情况=履约失败</b>（二值，履约失败＝被考核），问题类型用列 F工单判罚类型（本样例全空→「(未分类)」）；
    /// 网点=F承包区名称(全为太仓，命中种子)。
    /// </summary>
    [SkippableFact]
    public Task HomeDelivery_Unify_FilteredFailed_ColumnProblem_MatchesNetwork_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "送货上门明细",
            StgTable: ShentongSourceMap.HomeDeliveryTable,
            SampleFile: "送货上门达成分析历史详情导出_导出任务_20260617_109358147.xlsx",
            Flow: 2316, Stage: 5116, Rule: 3116,
            Domain: "虚假签收履约",
            FilterColumn: "F履约情况", FilterValue: "履约失败",
            ProblemTypeColumn: "F工单判罚类型",
            AmountColumn: null, ExpectAmount: false,
            AssertNetworkMatch: true,
            AssertEmployeeStatus9: false));

    // ─────────────────────────────────────────────────────────────
    // 通用驱动：导入 → 归一 → 断言 → 幂等 → 自清
    // ─────────────────────────────────────────────────────────────

    private sealed record SourceCase(
        string Desc,
        string StgTable,
        string SampleFile,
        long Flow, long Stage, long Rule,
        string Domain,
        string? FilterColumn, string? FilterValue,
        string ProblemTypeColumn,
        string? AmountColumn, bool ExpectAmount,
        bool AssertNetworkMatch,
        bool AssertEmployeeStatus9);

    private async Task RunSourceAsync(SourceCase c)
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        var path = global::System.IO.Path.Combine(明细Dir, c.SampleFile);
        Skip.IfNot(File.Exists(path), $"样例文件不存在: {path}");

        await using var db = CreateDbContext(conn);
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }

        CardFlowSeeder.Migrate(db);
        QualityUnifySeeder.EnsureTables(db);

        long batchId = 0;
        bool seededNetwork = false;
        try
        {
            await CleanupQualityAsync(conn);
            seededNetwork = await EnsureNetworkAsync(db);

            // ── Arrange：导入样例到对应 STG ──
            (batchId, var importResult) = await ImportOnceAsync(db, conn, path, c.Flow, c.Stage, c.Rule);
            Assert.True(importResult.Success, $"[{c.Desc}] 导入应成功，实际 Message={importResult.Message}");

            var stgRows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{c.StgTable}] WHERE [F批次ID] = @b", ("@b", batchId));
            _log.WriteLine($"[{c.Desc}] STG 导入行数 = {stgRows}");
            Assert.True(stgRows > 0, $"[{c.Desc}] 样例应至少导入 1 行 STG");

            // 期望事件数：过滤源 = 满足「过滤列=过滤值」的 STG 行数；整源源 = 全部 STG 行数
            int expectedEvents;
            if (c.FilterColumn != null && c.FilterValue != null)
            {
                expectedEvents = await CountAsync(conn,
                    $"SELECT COUNT(*) FROM [{c.StgTable}] WHERE [F批次ID] = @b AND [{c.FilterColumn}] = @v",
                    ("@b", batchId), ("@v", c.FilterValue));
                _log.WriteLine($"[{c.Desc}] 过滤源：满足 [{c.FilterColumn}]=N'{c.FilterValue}' 的 STG 行数 = {expectedEvents}（共 {stgRows} 行）");
                Assert.True(expectedEvents > 0, $"[{c.Desc}] 过滤后应至少有 1 行满足条件（否则样例无法验证过滤生效）");
                Assert.True(expectedEvents < stgRows, $"[{c.Desc}] 过滤应严格筛除部分行（否则过滤未生效），实际 {expectedEvents}/{stgRows}");
            }
            else
            {
                expectedEvents = stgRows;
                _log.WriteLine($"[{c.Desc}] 整源入：期望事件数 = 全部 STG 行数 = {expectedEvents}");
            }

            // ── Act：归一 ──
            var svc = new QualityUnificationService(db, new MasterDataMatcher(db));
            var result = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[{c.Desc}][首跑] EventsUpserted={result.EventsUpserted} NetUnmatched={result.NetworkUnmatched} EmpUnmatched={result.EmployeeUnmatched}");

            // ── Assert 1：本源本批事件行数 > 0 且 == 期望（过滤生效 / 整源全入）──
            var eventRows = await EventRowsAsync(conn, c.StgTable, batchId);
            _log.WriteLine($"[{c.Desc}] 本源本批事件行数 = {eventRows}（期望 {expectedEvents}）");
            Assert.True(eventRows > 0, $"[{c.Desc}] 通用事件路径应产出事件");
            Assert.Equal(expectedEvents, eventRows);

            // ── Assert 2：F质量域 正确 ──
            var wrongDomain = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND [F质量域] <> @dom",
                ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId), ("@dom", c.Domain));
            Assert.Equal(0, wrongDomain);

            // ── Assert 3：问题类型名称 正确（列源：事件 distinct == STG distinct(列)，空单元格→「(未分类)」）──
            var evtProblems = await DistinctEventProblemNamesAsync(conn, c.StgTable, batchId);
            _log.WriteLine($"[{c.Desc}] 事件 distinct F问题类型名称 = [{string.Join(",", evtProblems)}]");
            var expectedNames = await ExpectedColumnProblemNamesAsync(conn, c.StgTable, c.ProblemTypeColumn, batchId, c.FilterColumn, c.FilterValue);
            _log.WriteLine($"[{c.Desc}] STG 期望问题类型名称（空→(未分类)）= [{string.Join(",", expectedNames)}]");
            Assert.Equal(expectedNames.OrderBy(x => x), evtProblems.OrderBy(x => x));

            // ── Assert 4：网点匹配（仅 AssertNetworkMatch 源）：本源网点按名称/编码匹配到种子网点 → 全部状态1/编码320288 ──
            if (c.AssertNetworkMatch)
            {
                var netOk = await CountAsync(conn,
                    $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND [F网点编码] = @code AND [F网点匹配状态] = 1",
                    ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId), ("@code", NetCode));
                _log.WriteLine($"[{c.Desc}] 网点编码={NetCode}/状态=1 行数 = {netOk}（期望 {eventRows}）");
                Assert.Equal(eventRows, netOk);
            }
            else
            {
                // 无可命中本地种子的网点：断言全部未匹配（状态0），证明描述符取的网点列确为「外部/空」而非误命中。
                var netUnmatched = await CountAsync(conn,
                    $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND [F网点匹配状态] = 0",
                    ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId));
                _log.WriteLine($"[{c.Desc}] 网点状态=0（未命中本地种子，符合预期）行数 = {netUnmatched}（期望 {eventRows}）");
                Assert.Equal(eventRows, netUnmatched);
            }

            // ── Assert 5：F关键字段JSON 全非空 ──
            var emptyJson = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND ([F关键字段JSON] IS NULL OR LTRIM(RTRIM([F关键字段JSON])) = N'')",
                ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId));
            Assert.Equal(0, emptyJson);

            // ── Assert 6：来源行ID 全部能反查本批 STG（非孤儿）──
            var orphan = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{EventTable}] e WHERE e.[FOrgId] = @org AND e.[F来源STG表] = @tbl AND e.[F来源批次ID] = @b
                   AND NOT EXISTS (SELECT 1 FROM [{c.StgTable}] s WHERE s.[FID] = e.[F来源行ID] AND s.[F批次ID] = @b)",
                ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId));
            Assert.Equal(0, orphan);

            // ── Assert 7：无员工维度源（投诉账单）：全部 F员工匹配状态=9 且 工号/ID/姓名原文 全为空 ──
            if (c.AssertEmployeeStatus9)
            {
                var status9 = await CountAsync(conn,
                    $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND [F员工匹配状态] = 9",
                    ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId));
                _log.WriteLine($"[{c.Desc}] F员工匹配状态=9 行数 = {status9}（期望 {eventRows}）");
                Assert.Equal(eventRows, status9);

                var empNonEmpty = await CountAsync(conn,
                    $@"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b
                       AND ([F员工工号] IS NOT NULL OR [F员工ID] IS NOT NULL OR ([F员工姓名原文] IS NOT NULL AND LTRIM(RTRIM([F员工姓名原文])) <> N''))",
                    ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId));
                _log.WriteLine($"[{c.Desc}] 员工三字段任一非空行数 = {empNonEmpty}（期望 0：无员工维度全空）");
                Assert.Equal(0, empNonEmpty);
            }

            // ── Assert 8：金额（仅带金额列的源）：F考核金额 全非空且与源 [金额列] 数值一致 ──
            if (c.ExpectAmount && c.AmountColumn != null)
            {
                var emptyAmount = await CountAsync(conn,
                    $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND [F考核金额] IS NULL",
                    ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId));
                _log.WriteLine($"[{c.Desc}] F考核金额 为空行数 = {emptyAmount}（期望 0）");
                Assert.Equal(0, emptyAmount);

                var amountMatch = await CountAsync(conn,
                    $@"SELECT COUNT(*) FROM [{EventTable}] e
                       JOIN [{c.StgTable}] s ON s.[FID] = e.[F来源行ID] AND s.[F批次ID] = @b
                       WHERE e.[FOrgId] = @org AND e.[F来源STG表] = @tbl AND e.[F来源批次ID] = @b
                         AND e.[F考核金额] = TRY_CONVERT(DECIMAL(18,4), s.[{c.AmountColumn}])",
                    ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId));
                _log.WriteLine($"[{c.Desc}] F考核金额 与源 [{c.AmountColumn}] 一致行数 = {amountMatch}（期望 {eventRows}）");
                Assert.Equal(eventRows, amountMatch);

                var positiveAmount = await CountAsync(conn,
                    $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND [F考核金额] > 0",
                    ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId));
                _log.WriteLine($"[{c.Desc}] F考核金额 > 0 行数 = {positiveAmount}");
                Assert.True(positiveAmount > 0, $"[{c.Desc}] 应至少有 1 行考核金额 > 0（金额真有落值）");
            }

            // ── Assert 9：幂等：再跑一次 → 本源事件行数不变 ──
            var result2 = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[{c.Desc}][复跑] EventsUpserted={result2.EventsUpserted}");
            var eventRows2 = await EventRowsAsync(conn, c.StgTable, batchId);
            _log.WriteLine($"[{c.Desc}][幂等] 复跑后本源事件行数 = {eventRows2}（期望 {eventRows}）");
            Assert.Equal(eventRows, eventRows2);
        }
        finally
        {
            await CleanupQualityAsync(conn);
            await CleanupBatchAsync(conn, c.StgTable, batchId);
            if (seededNetwork) await CleanupNetworkAsync(conn);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 查询 helper
    // ─────────────────────────────────────────────────────────────

    private static Task<int> EventRowsAsync(string conn, string stgTable, long batchId) =>
        CountAsync(conn,
            $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b",
            ("@org", OrgId), ("@tbl", stgTable), ("@b", batchId));

    /// <summary>本源本批事件 distinct 非空 F问题类型名称。</summary>
    private static async Task<List<string>> DistinctEventProblemNamesAsync(string conn, string stgTable, long batchId)
    {
        var list = new List<string>();
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText =
            $"SELECT DISTINCT [F问题类型名称] FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND [F问题类型名称] IS NOT NULL";
        cmd.Parameters.AddWithValue("@org", OrgId);
        cmd.Parameters.AddWithValue("@tbl", stgTable);
        cmd.Parameters.AddWithValue("@b", batchId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetString(0).Trim());
        return list;
    }

    /// <summary>
    /// 列源期望问题类型名称集合：STG 本批（含过滤条件）该列 distinct 值，空白单元格映射为「(未分类)」。
    /// 与归一服务一致：值 Trim 后空字符串 → 字典名「(未分类)」。
    /// </summary>
    private static async Task<List<string>> ExpectedColumnProblemNamesAsync(
        string conn, string stgTable, string column, long batchId, string? filterCol, string? filterVal)
    {
        var set = new HashSet<string>();
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        var where = "[F批次ID] = @b";
        if (filterCol != null && filterVal != null) { where += $" AND [{filterCol}] = @v"; }
        cmd.CommandText = $"SELECT [{column}] FROM [{stgTable}] WHERE {where}";
        cmd.Parameters.AddWithValue("@b", batchId);
        if (filterCol != null && filterVal != null) cmd.Parameters.AddWithValue("@v", filterVal);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var raw = reader.IsDBNull(0) ? "" : (reader.GetValue(0)?.ToString() ?? "");
            raw = raw.Trim();
            set.Add(string.IsNullOrEmpty(raw) ? "(未分类)" : raw);
        }
        return set.ToList();
    }

    // ─────────────────────────────────────────────────────────────
    // Arrange / Cleanup helper（与批次1/2 同口径）
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
        var dir = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "stqc_batch3_snap");
        Directory.CreateDirectory(dir);
        var dst = global::System.IO.Path.Combine(dir, global::System.IO.Path.GetFileName(srcPath));
        using (var src = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var outFs = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            src.CopyTo(outFs);
        }
        return dst;
    }

    private static async Task<bool> EnsureNetworkAsync(STOTOPDbContext db)
    {
        var exists = await db.Set<ExpNetworkPoint>()
            .IgnoreQueryFilters()
            .AnyAsync(np => np.FCode == NetCode && np.FOrgId == OrgId);
        if (exists) return false;

        db.Set<ExpNetworkPoint>().Add(new ExpNetworkPoint
        {
            FCode = NetCode,
            FFullName = NetFullName,
            FOrgId = OrgId,
            FOwnerOrgId = OrgId,
        });
        await db.SaveChangesAsync();
        return true;
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
            FBatchNo = $"TESTC3B3-{Guid.NewGuid():N}",
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
        await ExecAsync(conn, $"DELETE FROM [{EventTable}] WHERE [FOrgId] = @org", ("@org", OrgId));
        await ExecAsync(conn, $"DELETE FROM [{DictTable}] WHERE [FOrgId] = @org", ("@org", OrgId));
    }

    private static async Task CleanupNetworkAsync(string conn)
    {
        await ExecAsync(conn,
            "DELETE FROM [EXP快递网点] WHERE [F编号] = @code AND [F组织ID] = @org",
            ("@code", NetCode), ("@org", OrgId));
    }

    private async Task CleanupBatchAsync(string conn, string table, long batchId)
    {
        if (batchId <= 0) return;
        try { await ExecAsync(conn, $"DELETE FROM [{table}] WHERE [F批次ID] = @b", ("@b", batchId)); }
        catch { /* ignore */ }
        try
        {
            await using var cleanup = CreateDbContext(conn);
            var b = await cleanup.Set<CfBatch>().FirstOrDefaultAsync(x => x.FID == batchId);
            if (b != null)
            {
                cleanup.Set<CfBatch>().Remove(b);
                await cleanup.SaveChangesAsync();
            }
        }
        catch { /* ignore */ }
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
