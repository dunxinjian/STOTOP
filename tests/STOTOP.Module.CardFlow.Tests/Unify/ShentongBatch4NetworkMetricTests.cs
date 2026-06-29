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
/// Plan2 Phase C — C3 批次4：归一服务「网点指标类 6 个汇总源 → 网点日质量指标（各填互不相交字段子集，按 网点×日 合并 upsert）」集成测试（连开发测试库 stotop）。
/// 仿 <see cref="UnifyBacklogMonitorTests"/>。6 源：物流信息及时/完整/准确汇总（无网点编码列，按名称匹配）、揽收/出仓/交货滞留考核汇总（有网点编码）。
///
/// 6 源样例都属网点 江苏太仓市城区公司(320288)；日期：及时/完整/准确/出仓=2026-06-16，揽收/滞留=2026-06-15。
/// 故归一后会形成 2 个 (网点×日) 网点日指标行：
///   2026-06-16 行 — 及时(揽收/派件/签收上传不及时率) + 完整(揽收/派件/到件缺失率) + 准确(不准确率/到件不准确率) + 出仓(一频次出仓及时率/未及时出仓量/出仓预估考核金额) 四源合并；
///   2026-06-15 行 — 揽收(及时揽收率/未及时揽收量) + 滞留(滞留率/考核滞留量/滞留预估考核金额) 两源合并。
///
/// 断言（每源「子集非空」+「按网点×日建/更行」+「合并不覆盖」+「幂等」）：
///   1) 6 源字段子集分别落值非空；
///   2) 合并不覆盖回归：预置一条 2026-06-16 同网点行手填「非本源字段」F滞留率=88.88（属滞留源，6-16 行无滞留源写它）→ 跑后仍 88.88，且 6-16 行四源子集被填；
///   3) 多源天然合并：2026-06-16 一行同时承载 及时+完整+准确+出仓 四源子集（互不相交、未互相清空）；2026-06-15 一行同时承载 揽收+滞留；
///   4) NetworkMetricUpserts > 0；
///   5) 幂等：再跑一次两行仍各 1、子集值不变、哨兵仍在。
/// 清理：try/finally 删本 org 网点日指标 + 各 STG 行（按网点编码/名称）+ CfBatch + 种子网点。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class ShentongBatch4NetworkMetricTests
{
    private const long OrgId = 192;
    private const string NetCode = "320288";
    private const string NetFullName = "江苏太仓市城区公司";
    private const string MetricTable = "QL申通_网点日质量指标";

    private static readonly DateTime Date16 = new(2026, 6, 16); // 及时/完整/准确/出仓
    private static readonly DateTime Date15 = new(2026, 6, 15); // 揽收/滞留

    private static readonly string 展示页Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据展示页";

    // (描述, 文件名, flow, stage, rule, STG 表名)
    private const string InfoFile = "excel（物流信息指数）.xls";
    private const string OutboundFile = "出仓考核汇总导出_导出任务_20260617_109361069.xlsx";
    private const string PickupFile = "订单揽收考核汇总分析导出_v2-edd7c2a622b344b88dfb49f63a02a8af.xlsx";
    private const string HandoverFile = "网点交货滞留v3汇总导出STO-4b64cda4b3b04eef92514da19b62ce7a.xlsx";

    private const string TimelyTable = "STG申通_物流信息及时汇总";
    private const string CompleteTable = "STG申通_物流信息完整汇总";
    private const string AccurateTable = "STG申通_物流信息准确汇总";
    private const string OutboundTable = "STG申通_出仓考核汇总";
    private const string PickupTable = "STG申通_揽收考核汇总";
    private const string HandoverTable = "STG申通_交货滞留汇总";

    private readonly ITestOutputHelper _log;
    public ShentongBatch4NetworkMetricTests(ITestOutputHelper log) => _log = log;

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
        var dir = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "stqc_unify_b4_snap");
        Directory.CreateDirectory(dir);
        var dst = global::System.IO.Path.Combine(dir, global::System.IO.Path.GetFileName(srcPath));
        using (var src = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var outFs = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            src.CopyTo(outFs);
        }
        return dst;
    }

    [SkippableFact]
    public async Task Unify_Batch4_SixNetworkMetricSources_SubsetsFilled_MergeNoOverwrite_Idempotent()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        var infoPath = global::System.IO.Path.Combine(展示页Dir, InfoFile);
        var outboundPath = global::System.IO.Path.Combine(展示页Dir, OutboundFile);
        var pickupPath = global::System.IO.Path.Combine(展示页Dir, PickupFile);
        var handoverPath = global::System.IO.Path.Combine(展示页Dir, HandoverFile);
        Skip.IfNot(File.Exists(infoPath), $"样例文件不存在: {infoPath}");
        Skip.IfNot(File.Exists(outboundPath), $"样例文件不存在: {outboundPath}");
        Skip.IfNot(File.Exists(pickupPath), $"样例文件不存在: {pickupPath}");
        Skip.IfNot(File.Exists(handoverPath), $"样例文件不存在: {handoverPath}");

        await using var db = CreateDbContext(conn);
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }

        CardFlowSeeder.Migrate(db);
        QualityUnifySeeder.EnsureTables(db);

        var batchIds = new List<(long id, string table)>();
        bool seededNetwork = false;
        try
        {
            // 预清：删本 org 网点日指标 + 各 STG 残留（按网点编码/名称，避免跨批次去重把本次导入吞掉）
            await CleanupMetricAsync(conn);
            await CleanupStgResidueAsync(conn);

            // 种子网点（编码 320288 + 全称 江苏太仓市城区公司）：有编码源按编码命中，无编码源按名称命中。
            seededNetwork = await EnsureNetworkAsync(db);

            // ── 合并不覆盖回归：预置一条 2026-06-16 同网点行，手填「非本源字段」F滞留率=88.88 ──
            // 滞留属 2026-06-15 滞留源；2026-06-16 行无任何本批源写 F滞留率 → 跑后应保持 88.88。
            await ExecAsync(conn,
                $@"INSERT INTO [{MetricTable}] ([FOrgId],[F承运商],[F业务日期],[F网点编码],[F滞留率],[F创建时间])
                   VALUES (@org, N'申通', @d, @code, 88.88, GETDATE())",
                ("@org", OrgId), ("@d", Date16), ("@code", NetCode));

            // ── 导入 6 个 STG（物流信息指数同文件三 sheet 三规则；出仓/揽收/滞留各一）──
            // flow/stage/rule（来自 CardFlowSeeder）：及时 2319/5119/3119、完整 2320/5120/3120、准确 2321/5121/3121、
            //   出仓 2323/5123/3123、揽收考核 2329/5129/3129、滞留汇总 2327/5127/3127。
            await ImportAndExpect(db, conn, batchIds, infoPath, 2319, 5119, 3119, TimelyTable, 1);
            await ImportAndExpect(db, conn, batchIds, infoPath, 2320, 5120, 3120, CompleteTable, 2); // 完整 2 行（含 江苏太仓城区市场二服务点）
            await ImportAndExpect(db, conn, batchIds, infoPath, 2321, 5121, 3121, AccurateTable, 1);
            await ImportAndExpect(db, conn, batchIds, outboundPath, 2323, 5123, 3123, OutboundTable, 1);
            await ImportAndExpect(db, conn, batchIds, pickupPath, 2329, 5129, 3129, PickupTable, 1);
            await ImportAndExpect(db, conn, batchIds, handoverPath, 2327, 5127, 3127, HandoverTable, 1);

            // ── 行为：归一（泛化分发同时跑事件/员工/网点指标类；本测试断言 6 网点指标源产物）──
            var svc = new QualityUnificationService(db, new MasterDataMatcher(db));
            var result = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[首跑] NetMetricUpserts={result.NetworkMetricUpserts} NetUnmatched={result.NetworkUnmatched}");
            Assert.True(result.NetworkMetricUpserts > 0, $"应有网点指标 upsert，实际 {result.NetworkMetricUpserts}");

            // ── 断言：2026-06-16 行 四源子集分别非空（及时/完整/准确/出仓）──
            await AssertNotNull16(conn, "F揽收上传不及时率");
            await AssertNotNull16(conn, "F派件上传不及时率");
            await AssertNotNull16(conn, "F签收上传不及时率");
            await AssertNotNull16(conn, "F揽收缺失率");
            await AssertNotNull16(conn, "F派件缺失率");
            await AssertNotNull16(conn, "F到件缺失率");
            await AssertNotNull16(conn, "F不准确率");
            await AssertNotNull16(conn, "F到件不准确率");
            await AssertNotNull16(conn, "F一频次出仓及时率");
            await AssertNotNull16(conn, "F未及时出仓量");
            await AssertNotNull16(conn, "F出仓预估考核金额");

            // ── 断言：2026-06-15 行 两源子集分别非空（揽收/滞留）──
            await AssertNotNull15(conn, "F及时揽收率");
            await AssertNotNull15(conn, "F未及时揽收量");
            // 滞留源子集：本样例仅 F滞留率(0.52) 非空；F考核滞留量 与 F滞留预估考核金额（源列「考核滞留量」「滞留预估考核-日」）
            // 本样例单元格为空字符串 → TryInt/TryDecimal 解析为 null（正确，无值不应造值）。故仅强断 F滞留率 非空。
            await AssertNotNull15(conn, "F滞留率");

            // ── 断言：率单位口径（率存百分数，% 仅去符号不除100）──
            // 揽收 及时揽收率=99.68（无尾缀纯数值，原样）；出仓 一频次出仓及时率=85.78%（去% →85.78）。
            var 及时揽收率 = await ScalarDecimalAsync(conn,
                $"SELECT [F及时揽收率] FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code",
                ("@org", OrgId), ("@d", Date15), ("@code", NetCode));
            var 出仓及时率 = await ScalarDecimalAsync(conn,
                $"SELECT [F一频次出仓及时率] FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code",
                ("@org", OrgId), ("@d", Date16), ("@code", NetCode));
            _log.WriteLine($"[率单位] F及时揽收率={及时揽收率}(期望≈99.68) F一频次出仓及时率={出仓及时率}(期望≈85.78)");
            Assert.NotNull(及时揽收率);
            Assert.NotNull(出仓及时率);
            Assert.True(Math.Abs(及时揽收率!.Value - 99.68m) < 0.01m, $"及时揽收率应≈99.68，实际 {及时揽收率}");
            Assert.True(Math.Abs(出仓及时率!.Value - 85.78m) < 0.01m, $"一频次出仓及时率应≈85.78，实际 {出仓及时率}");

            // ── 断言：合并不覆盖回归——预置的 F滞留率=88.88 在 2026-06-16 行仍保持（本批 6-16 无源写它）──
            var keepSentinel = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code AND [F滞留率]=88.88",
                ("@org", OrgId), ("@d", Date16), ("@code", NetCode));
            _log.WriteLine($"[合并不覆盖] 2026-06-16 行 F滞留率=88.88 保持 行数={keepSentinel}（期望1，证明非整行覆盖）");
            Assert.Equal(1, keepSentinel);

            // ── 断言：行数——本批形成恰 2 个 (网点×日) 行（6-16 / 6-15）──
            var totalRows = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F网点编码]=@code",
                ("@org", OrgId), ("@code", NetCode));
            _log.WriteLine($"[行数] (网点×日) 行数={totalRows}（期望2：6-16/6-15）");
            Assert.Equal(2, totalRows);

            // ── 断言：多源天然合并——2026-06-16 一行同时承载 及时+完整+准确+出仓 四源（互不相交未互相清空）──
            var merge16 = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{MetricTable}]
                   WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code
                     AND [F揽收上传不及时率] IS NOT NULL AND [F揽收缺失率] IS NOT NULL
                     AND [F不准确率] IS NOT NULL AND [F一频次出仓及时率] IS NOT NULL",
                ("@org", OrgId), ("@d", Date16), ("@code", NetCode));
            _log.WriteLine($"[多源合并] 6-16 行 及时+完整+准确+出仓 四源子集同行非空 行数={merge16}（期望1）");
            Assert.Equal(1, merge16);

            // ── 断言：多源天然合并——2026-06-15 一行同时承载 揽收(及时揽收率/未及时揽收量) + 滞留(滞留率) 两源 ──
            var merge15 = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{MetricTable}]
                   WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code
                     AND [F及时揽收率] IS NOT NULL AND [F未及时揽收量] IS NOT NULL AND [F滞留率] IS NOT NULL",
                ("@org", OrgId), ("@d", Date15), ("@code", NetCode));
            _log.WriteLine($"[多源合并] 6-15 行 揽收+滞留 两源子集同行非空 行数={merge15}（期望1）");
            Assert.Equal(1, merge15);

            // ── 幂等：再跑一次 → 行数仍 2、哨兵仍在、四源子集仍非空 ──
            var result2 = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[复跑] NetMetricUpserts={result2.NetworkMetricUpserts}");

            var totalAfter = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F网点编码]=@code",
                ("@org", OrgId), ("@code", NetCode));
            Assert.Equal(2, totalAfter);

            var keepSentinelAfter = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code AND [F滞留率]=88.88",
                ("@org", OrgId), ("@d", Date16), ("@code", NetCode));
            _log.WriteLine($"[幂等] 复跑后 6-16 行 F滞留率=88.88 保持 行数={keepSentinelAfter}（期望1）");
            Assert.Equal(1, keepSentinelAfter);

            var merge16After = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{MetricTable}]
                   WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code
                     AND [F揽收上传不及时率] IS NOT NULL AND [F一频次出仓及时率] IS NOT NULL",
                ("@org", OrgId), ("@d", Date16), ("@code", NetCode));
            Assert.Equal(1, merge16After);
        }
        finally
        {
            await CleanupMetricAsync(conn);
            foreach (var (id, table) in batchIds) await CleanupBatchAsync(conn, table, id);
            await CleanupStgResidueAsync(conn);
            if (seededNetwork) await ExecAsync(conn, "DELETE FROM [EXP快递网点] WHERE [F编号] = @code AND [F组织ID] = @org", ("@code", NetCode), ("@org", OrgId));
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 断言 helper
    // ─────────────────────────────────────────────────────────────

    private async Task AssertNotNull16(string conn, string col)
    {
        var n = await CountAsync(conn,
            $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code AND [{col}] IS NOT NULL",
            ("@org", OrgId), ("@d", Date16), ("@code", NetCode));
        _log.WriteLine($"[子集非空 6-16] [{col}] 非空行数={n}（期望1）");
        Assert.Equal(1, n);
    }

    private async Task AssertNotNull15(string conn, string col)
    {
        var n = await CountAsync(conn,
            $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code AND [{col}] IS NOT NULL",
            ("@org", OrgId), ("@d", Date15), ("@code", NetCode));
        _log.WriteLine($"[子集非空 6-15] [{col}] 非空行数={n}（期望1）");
        Assert.Equal(1, n);
    }

    // ─────────────────────────────────────────────────────────────
    // 安排 / 清理 helper
    // ─────────────────────────────────────────────────────────────

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

    /// <summary>导入一个文件到指定 STG 表并断言成功 + 行数；记录 batchId 供清理。</summary>
    private async Task ImportAndExpect(
        STOTOPDbContext db, string conn, List<(long, string)> batchIds,
        string path, long flow, long stage, long rule, string table, int expected)
    {
        var (id, result) = await ImportOnceAsync(db, conn, path, flow, stage, rule);
        batchIds.Add((id, table));
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
            FBatchNo = $"TESTC3B4-{Guid.NewGuid():N}",
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

    private static async Task CleanupMetricAsync(string conn)
    {
        await ExecAsync(conn, $"DELETE FROM [{MetricTable}] WHERE [FOrgId] = @org", ("@org", OrgId));
    }

    /// <summary>删 6 源 STG 残留行（按网点编码/名称，避免跨批次去重把本次导入吞掉——真库测试已知坑）。</summary>
    private static async Task CleanupStgResidueAsync(string conn)
    {
        // 有编码列的源按编码删；无编码列（物流信息汇总）按网点名称删。
        await ExecAsync(conn, $"DELETE FROM [{OutboundTable}] WHERE [FOrgId]=@org AND [F所属网点编码]=@code", ("@org", OrgId), ("@code", NetCode));
        await ExecAsync(conn, $"DELETE FROM [{PickupTable}] WHERE [FOrgId]=@org AND [F揽收所属网点编码]=@code", ("@org", OrgId), ("@code", NetCode));
        await ExecAsync(conn, $"DELETE FROM [{HandoverTable}] WHERE [FOrgId]=@org AND [F揽收所属网点编码]=@code", ("@org", OrgId), ("@code", NetCode));
        // 物流信息三汇总按 F网点名称 删（江苏太仓市城区公司 + 江苏太仓城区市场二服务点 等本文件涉及网点）
        await ExecAsync(conn, $"DELETE FROM [{TimelyTable}] WHERE [FOrgId]=@org AND [F网点名称]=@name", ("@org", OrgId), ("@name", NetFullName));
        await ExecAsync(conn, $"DELETE FROM [{CompleteTable}] WHERE [FOrgId]=@org AND ([F网点名称]=@name OR [F网点名称]=@name2)", ("@org", OrgId), ("@name", NetFullName), ("@name2", "江苏太仓城区市场二服务点"));
        await ExecAsync(conn, $"DELETE FROM [{AccurateTable}] WHERE [FOrgId]=@org AND [F网点名称]=@name", ("@org", OrgId), ("@name", NetFullName));
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

    private static async Task<decimal?> ScalarDecimalAsync(string conn, string sql, params (string name, object val)[] ps)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, val) in ps) cmd.Parameters.AddWithValue(name, val);
        var o = await cmd.ExecuteScalarAsync();
        return o is null or DBNull ? (decimal?)null : Convert.ToDecimal(o);
    }
}
