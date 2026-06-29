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
/// Plan2 Phase C — C3 批次1：5 个「通用事件源」归一集成测试（连开发测试库 stotop）。
/// 这 5 源都走通用事件路径 <see cref="QualityUnificationService.UnifyShentongAsync"/> →（内部）UnifyGenericEventAsync，
/// 仅靠 <see cref="ShentongSourceMap.All"/> 里新加的描述符驱动，无服务主干改动。
///
/// 每源一个 [SkippableFact]（失败隔离），统一 Arrange/Act/Assert：
///  Arrange：建 STG+QL 表；清本 org 既有 QL 事件/字典 + 5 源 STG 本网点残留（防跨批次 FID 撞号污染计数）；
///           种子 ExpNetworkPoint(320288=江苏太仓市城区公司,org=192)；用 Plan1 导入封装把该源样例导入对应 STG(org=192)。
///  Act：    svc.UnifyShentongAsync(192)（本源走通用路径；其它已实现源各自处理，互不干扰）。
///  Assert： 本源本批事件行数 > 0；
///           过滤源 → 事件数 == 满足过滤条件的 STG 行数（过滤生效）；整源源 → 事件数 == 全部 STG 行数；
///           F质量域 正确；问题类型名称正确（常量源=该常量；列源=STG distinct(列)，空单元格落「(未分类)」）；
///           网点 320288 匹配(状态1)；F关键字段JSON 全非空；再跑一次行数不变（幂等）。
///  Cleanup：try/finally 删本 org QL 事件/字典 + 本批 STG + 5 源 STG 本网点残留 + CfBatch + 种子网点。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class ShentongBatch1EventSourcesTests
{
    private const long OrgId = 192;
    private const string NetCode = "320288";
    private const string NetFullName = "江苏太仓市城区公司";
    private const string EventTable = "QL申通_承运商质量事件";
    private const string DictTable = "QL申通_质量问题字典";

    private static readonly string 明细Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细";

    private readonly ITestOutputHelper _log;
    public ShentongBatch1EventSourcesTests(ITestOutputHelper log) => _log = log;

    // ─────────────────────────────────────────────────────────────
    // 5 个 [SkippableFact]
    // ─────────────────────────────────────────────────────────────

    /// <summary>① 揽收分析明细：揽收时效，过滤 F揽收及时标识=否，问题类型常量「揽收不及时」。</summary>
    [SkippableFact]
    public Task PickupAnalysis_Unify_Filtered_ConstantProblem_MatchesNetwork_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "揽收分析明细",
            StgTable: ShentongSourceMap.PickupAnalysisTable,
            SampleFile: "订单揽收分析明细V3-1c76e08a7b4e4ccab6e43eef55a2ee94.xlsx",
            Flow: 2303, Stage: 5103, Rule: 3103,
            Domain: "揽收时效",
            FilterColumn: "F揽收及时标识", FilterValue: "否",
            ProblemTypeColumn: null, ProblemTypeConstant: "揽收不及时"));

    /// <summary>② 未出仓监控明细：出仓时效，整源入，问题类型常量「未及时出仓」。</summary>
    [SkippableFact]
    public Task OutboundMonitor_Unify_WholeSource_ConstantProblem_MatchesNetwork_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "未出仓监控明细",
            StgTable: ShentongSourceMap.OutboundMonitorTable,
            SampleFile: "未出仓实时监控导出_导出任务_20260617_109357559.xlsx",
            Flow: 2304, Stage: 5104, Rule: 3104,
            Domain: "出仓时效",
            FilterColumn: null, FilterValue: null,
            ProblemTypeColumn: null, ProblemTypeConstant: "未及时出仓"));

    /// <summary>③ 交货滞留明细：交货滞留，过滤 F交货滞留标识=是，问题类型常量「交货滞留」。</summary>
    [SkippableFact]
    public Task HandoverDelay_Unify_Filtered_ConstantProblem_MatchesNetwork_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "交货滞留明细",
            StgTable: ShentongSourceMap.HandoverDelayTable,
            SampleFile: "网点交货滞留v3明细导出STO-cb56d202ac2d45db84eb1f28e907307e.xlsx",
            Flow: 2305, Stage: 5105, Rule: 3105,
            Domain: "交货滞留",
            FilterColumn: "F交货滞留标识", FilterValue: "是",
            ProblemTypeColumn: null, ProblemTypeConstant: "交货滞留"));

    /// <summary>④ 末端派送考核明细：派送签收时效，整源入（一期口径），问题类型用列 F问题件类型名称（空→「(未分类)」）。</summary>
    [SkippableFact]
    public Task DeliveryAssess_Unify_WholeSource_ColumnProblem_MatchesNetwork_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "末端派送考核明细",
            StgTable: ShentongSourceMap.DeliveryAssessTable,
            SampleFile: "末端派送考核(新)明细V2-2262ba2ddacb46319941bc759695b916.xlsx",
            Flow: 2306, Stage: 5106, Rule: 3106,
            Domain: "派送签收时效",
            FilterColumn: null, FilterValue: null,
            ProblemTypeColumn: "F问题件类型名称", ProblemTypeConstant: null));

    /// <summary>⑤ 签收未达标明细：派送签收时效，整源入，问题类型常量「签收未达标」。</summary>
    [SkippableFact]
    public Task SignSubstandard_Unify_WholeSource_ConstantProblem_MatchesNetwork_Idempotent() =>
        RunSourceAsync(new SourceCase(
            Desc: "签收未达标明细",
            StgTable: ShentongSourceMap.SignSubstandardTable,
            SampleFile: "ossf0ba7cd6c1524ab8898e157103219f0f签收未达标.xlsx",
            Flow: 2307, Stage: 5107, Rule: 3107,
            Domain: "派送签收时效",
            FilterColumn: null, FilterValue: null,
            ProblemTypeColumn: null, ProblemTypeConstant: "签收未达标"));

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
        string? ProblemTypeColumn, string? ProblemTypeConstant);

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
            // 预清 STG 残留：删 5 个事件源里本网点的旧批次行，避免跨批次 FID 撞号污染本批 F来源批次ID 计数
            // （真库测试已知坑：UnifyGenericEventAsync 按「org+表名」读全部未撤销行、不按批次，残留旧行会被复跑重新处理）。
            await CleanupStgResidueAsync(conn);
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

            // ── Assert 3：问题类型名称 正确 ──
            var evtProblems = await DistinctEventProblemNamesAsync(conn, c.StgTable, batchId);
            _log.WriteLine($"[{c.Desc}] 事件 distinct F问题类型名称 = [{string.Join(",", evtProblems)}]");
            if (c.ProblemTypeColumn == null)
            {
                // 常量源：所有事件问题类型名称 == 常量
                Assert.Single(evtProblems);
                Assert.Equal(c.ProblemTypeConstant, evtProblems[0]);
            }
            else
            {
                // 列源：事件问题类型名称集合 == STG distinct(列)，其中空单元格映射为「(未分类)」
                var expectedNames = await ExpectedColumnProblemNamesAsync(conn, c.StgTable, c.ProblemTypeColumn, batchId, c.FilterColumn, c.FilterValue);
                _log.WriteLine($"[{c.Desc}] STG 期望问题类型名称（空→(未分类)）= [{string.Join(",", expectedNames)}]");
                Assert.Equal(expectedNames.OrderBy(x => x), evtProblems.OrderBy(x => x));
            }

            // ── Assert 4：网点 320288 匹配(状态1) —— 本源网点仅有名称，名称匹配到种子网点 → 全部状态1/编码320288 ──
            var netOk = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND [F网点编码] = @code AND [F网点匹配状态] = 1",
                ("@org", OrgId), ("@tbl", c.StgTable), ("@b", batchId), ("@code", NetCode));
            _log.WriteLine($"[{c.Desc}] 网点编码={NetCode}/状态=1 行数 = {netOk}（期望 {eventRows}）");
            Assert.Equal(eventRows, netOk);

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

            // ── Assert 7：幂等：再跑一次 → 本源事件行数不变 ──
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
            // 清 STG 残留（按网点删全部 5 源本网点行）：与预清同口径，确保本次运行不给后续跑/其它源留残留。
            await CleanupStgResidueAsync(conn);
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
    // Arrange / Cleanup helper（与 UnifyGenericEventTests 同口径）
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
        var dir = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "stqc_batch1_snap");
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
            FBatchNo = $"TESTC3B1-{Guid.NewGuid():N}",
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

    /// <summary>
    /// 删 5 个批次1事件源 STG 表里本测试网点（江苏太仓市城区公司）的残留行——真库测试已知坑。
    /// <para>
    /// 成因：<see cref="QualityUnificationService.UnifyShentongAsync"/> → UnifyGenericEventAsync 用 StgRawReader
    /// 按「org+表名」读<b>全部未撤销 STG 行（不按批次、无 ORDER BY）</b>，事件 upsert 键是 (FOrgId,承运商,来源STG表,来源行ID=FID)。
    /// 上一次中断运行（finally 未执行）残留的旧批次行若与本批行 FID 撞号，会和本批行映射到同一事件键，
    /// 读取顺序不确定 → 复跑时「最后写入者」可能把事件 F来源批次ID 改指向残留批次，使本批计数下滑（幂等断言假阴性）。
    /// </para>
    /// <para>
    /// 这 5 源描述符均无有效网点编码列（<c>NetworkCodeColumn=null</c>），归一按<b>网点名称精确匹配</b>种子网点；
    /// 种子仅设 FFullName=江苏太仓市城区公司，故凡被归到本网点的行其名称列恒等于该全称 → 按名称列删可精确命中全部残留。
    /// 导入前（清旧残留）+ finally（清本次行）都调用，确保归一只看到本批行。仿 ShentongBatch4NetworkMetricTests.CleanupStgResidueAsync。
    /// </para>
    /// </summary>
    private static async Task CleanupStgResidueAsync(string conn)
    {
        // 5 源 DELETE 共用同一组参数 (@org,@name)，合并到一条命令一次连接执行
        // （真库为远程，单次连接握手成本高；批量减少连接数/网络往返）。各源用其归一所用的网点名称列。
        var sql =
            $"DELETE FROM [{ShentongSourceMap.PickupAnalysisTable}] WHERE [FOrgId]=@org AND [F揽收所属网点]=@name;" +
            $"DELETE FROM [{ShentongSourceMap.OutboundMonitorTable}] WHERE [FOrgId]=@org AND [F应签所属网点]=@name;" +
            $"DELETE FROM [{ShentongSourceMap.HandoverDelayTable}] WHERE [FOrgId]=@org AND [F揽收所属网点]=@name;" +
            $"DELETE FROM [{ShentongSourceMap.DeliveryAssessTable}] WHERE [FOrgId]=@org AND [F应签收所属网点名称]=@name;" +
            $"DELETE FROM [{ShentongSourceMap.SignSubstandardTable}] WHERE [FOrgId]=@org AND [F应签网点]=@name;";
        await ExecAsync(conn, sql, ("@org", OrgId), ("@name", NetFullName));
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
