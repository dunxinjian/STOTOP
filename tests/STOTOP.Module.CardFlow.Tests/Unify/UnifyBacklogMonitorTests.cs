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
/// Plan2 Phase C — C2：归一服务「网点指标类（积压监控汇总 → 网点日质量指标）」集成测试（连开发测试库 stotop）。
/// 同时验证<b>多源合并 upsert 不覆盖</b>这一 C3 复用模板。
///
/// 安排：EnsureTables 建 QL 表 + CardFlowSeeder.Migrate 建 STG 表；用规则 3126 把积压监控样例
///   导入 STG申通_积压监控汇总(orgId=192，文件含 1 数据行：网点 320288 / 日期 20260615）；
///   种子 ExpNetworkPoint(FCode=320288) 使该网点按编码匹配；
///   预先插入一条同 网点×日 的网点日指标行并手填一个「非本源字段」F未及时出仓量=99（出仓属 C3 源，本源不碰）。
/// 行为：svc.UnifyShentongAsync(192)（泛化分发同时跑事件类/员工指标类/网点指标类；本测试断言网点指标类产物）。
/// 断言：
///   1) 网点日指标按 网点编码×业务日期(2026-06-15) 落值——积压倍数/遗失率ppm/进港投诉量/日均出港量等积压字段非 null；
///   2) 只填子集：出仓/滞留等非本源字段为 null（除被回归预置的 99）；
///   3) 合并不覆盖：预置的 F未及时出仓量 仍 = 99（积压监控源没碰它），同时积压字段被填上；
///   4) NetworkMetricUpserts > 0；
///   5) 幂等：再跑一次行数不变。
/// 清理：try/finally 删本测试 org 的网点日指标 + STG 行 + CfBatch + 种子网点。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class UnifyBacklogMonitorTests
{
    private const long OrgId = 192;
    private const string NetCode = "320288";
    private const string NetFullName = "江苏太仓市城区公司";
    private const string MetricTable = "QL申通_网点日质量指标";
    private const string StgTable = "STG申通_积压监控汇总";
    private static readonly DateTime BizDate = new(2026, 6, 15); // 文件 F统计日期=20260615 → 2026-06-15

    private static readonly string 展示页Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据展示页";
    private const string BacklogFile = "积压异常监控_导出任务_20260617_109361094.xlsx";

    private readonly ITestOutputHelper _log;
    public UnifyBacklogMonitorTests(ITestOutputHelper log) => _log = log;

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

    /// <summary>
    /// 快照样例文件到临时目录（源文件可能被 Excel 打开，插件 FileShare.Read 会被独占锁拒绝）。
    /// </summary>
    private static string Snapshot(string srcPath)
    {
        var dir = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "stqc_unify_snap");
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
    public async Task Unify_BacklogMonitor_BuildsNetworkMetric_SubsetOnly_MergeNoOverwrite_Idempotent()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        var path = global::System.IO.Path.Combine(展示页Dir, BacklogFile);
        Skip.IfNot(File.Exists(path), $"样例文件不存在: {path}");

        await using var db = CreateDbContext(conn);
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }

        // 建 STG 表 + QL 表
        CardFlowSeeder.Migrate(db);
        QualityUnifySeeder.EnsureTables(db);

        long batchId = 0;
        bool seededNetwork = false;
        try
        {
            // 预清：删本 org 网点日指标 + 残留 STG（按网点+日期，避免跨批次去重把本次导入吞掉）
            await CleanupMetricAsync(conn);
            await ExecAsync(conn, $"DELETE FROM [{StgTable}] WHERE [FOrgId] = @org AND [F网点编码] = @code", ("@org", OrgId), ("@code", NetCode));

            // 种子网点（按编码匹配的目标）
            seededNetwork = await EnsureNetworkAsync(db);

            // ── 合并不覆盖回归：预置一条同 网点×日 的网点日指标行，手填「非本源字段」F未及时出仓量=99 ──
            await ExecAsync(conn,
                $@"INSERT INTO [{MetricTable}] ([FOrgId],[F承运商],[F业务日期],[F网点编码],[F未及时出仓量],[F创建时间])
                   VALUES (@org, N'申通', @d, @code, 99, GETDATE())",
                ("@org", OrgId), ("@d", BizDate), ("@code", NetCode));

            // ── 导入 STG（1 行，规则 3126 / 流程 2326 / 首节点 5126，headerRow=1 由规则配置）──
            (batchId, var importResult) = await ImportOnceAsync(db, conn, path, 2326, 5126, 3126);
            Assert.True(importResult.Success, $"导入应成功，实际 Message={importResult.Message}");
            var stgRows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{StgTable}] WHERE [F批次ID] = @b", ("@b", batchId));
            _log.WriteLine($"[STG] 导入行数 = {stgRows}（期望 1）");
            Assert.Equal(1, stgRows);

            // ── 行为：归一（泛化分发；网点指标类路径产出）──
            var svc = new QualityUnificationService(db, new MasterDataMatcher(db));
            var result = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[首跑] NetMetricUpserts={result.NetworkMetricUpserts} NetUnmatched={result.NetworkUnmatched}");
            Assert.True(result.NetworkMetricUpserts > 0, $"应有网点指标 upsert，实际 {result.NetworkMetricUpserts}");

            // ── 断言 1：网点日指标按 网点×日 落积压子集字段（非 null）──
            // 同时纳入 F进港投诉率/F虚签投诉率（万分号源列，曾被 decimal.TryParse 判 false 落 null 丢数据）。
            var subsetOk = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{MetricTable}]
                   WHERE [FOrgId] = @org AND [F承运商] = N'申通' AND [F业务日期] = @d AND [F网点编码] = @code
                     AND [F积压倍数] IS NOT NULL AND [F遗失率ppm] IS NOT NULL
                     AND [F进港投诉量] IS NOT NULL AND [F日均出港量] IS NOT NULL
                     AND [F进港投诉率] IS NOT NULL AND [F虚签投诉率] IS NOT NULL",
                ("@org", OrgId), ("@d", BizDate), ("@code", NetCode));
            _log.WriteLine($"[积压子集] 积压倍数/遗失率ppm/进港投诉量/日均出港量/进港投诉率/虚签投诉率 非空行数 = {subsetOk}（期望 1）");
            Assert.Equal(1, subsetOk);

            // ── 断言 1b：万分号折算口径（率以「百分数数值」存）——文件 进港投诉率=6.4‱ → 0.064；虚签投诉率=3.69‱ → 0.0369 ──
            // 用容差比较，避免浮点/精度脆弱；这两列若未支持万分号会落 null（上面断言已挡），此处再确认折算值正确。
            var 进港投诉率 = await ScalarDecimalAsync(conn,
                $@"SELECT [F进港投诉率] FROM [{MetricTable}]
                   WHERE [FOrgId] = @org AND [F业务日期] = @d AND [F网点编码] = @code",
                ("@org", OrgId), ("@d", BizDate), ("@code", NetCode));
            var 虚签投诉率 = await ScalarDecimalAsync(conn,
                $@"SELECT [F虚签投诉率] FROM [{MetricTable}]
                   WHERE [FOrgId] = @org AND [F业务日期] = @d AND [F网点编码] = @code",
                ("@org", OrgId), ("@d", BizDate), ("@code", NetCode));
            _log.WriteLine($"[万分号折算] F进港投诉率={进港投诉率}（期望≈0.064），F虚签投诉率={虚签投诉率}（期望≈0.0369）");
            Assert.NotNull(进港投诉率);
            Assert.NotNull(虚签投诉率);
            Assert.True(Math.Abs(进港投诉率!.Value - 0.064m) < 0.0001m, $"F进港投诉率 应≈0.064，实际 {进港投诉率}");
            Assert.True(Math.Abs(虚签投诉率!.Value - 0.0369m) < 0.0001m, $"F虚签投诉率 应≈0.0369，实际 {虚签投诉率}");

            // ── 断言 2：只填子集——非本源「滞留」字段为 null（出仓被回归预置=99，单列下面专测）──
            var stayNull = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{MetricTable}]
                   WHERE [FOrgId] = @org AND [F业务日期] = @d AND [F网点编码] = @code
                     AND [F滞留率] IS NULL AND [F考核滞留量] IS NULL AND [F一频次出仓及时率] IS NULL",
                ("@org", OrgId), ("@d", BizDate), ("@code", NetCode));
            _log.WriteLine($"[未填子集] 滞留率/考核滞留量/出仓及时率 仍为 null 行数 = {stayNull}（期望 1）");
            Assert.Equal(1, stayNull);

            // ── 断言 3：合并不覆盖——预置的 F未及时出仓量 仍 = 99（积压监控源没碰它）──
            var keep99 = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{MetricTable}]
                   WHERE [FOrgId] = @org AND [F业务日期] = @d AND [F网点编码] = @code AND [F未及时出仓量] = 99",
                ("@org", OrgId), ("@d", BizDate), ("@code", NetCode));
            _log.WriteLine($"[合并不覆盖] F未及时出仓量=99 保持 行数 = {keep99}（期望 1，证明非整行覆盖）");
            Assert.Equal(1, keep99);

            // ── 断言 4：本批 upsert 落在唯一一行（合并到既有行，未新建重复行）──
            var totalRows = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId] = @org AND [F业务日期] = @d AND [F网点编码] = @code",
                ("@org", OrgId), ("@d", BizDate), ("@code", NetCode));
            _log.WriteLine($"[合并] (网点×日) 行数 = {totalRows}（期望 1，合并未新增行）");
            Assert.Equal(1, totalRows);

            // ── 幂等：再跑一次 → 行数不变、99 仍在、积压字段仍非空 ──
            var result2 = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[复跑] NetMetricUpserts={result2.NetworkMetricUpserts}");

            var totalAfter = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId] = @org AND [F业务日期] = @d AND [F网点编码] = @code",
                ("@org", OrgId), ("@d", BizDate), ("@code", NetCode));
            _log.WriteLine($"[幂等] 复跑后 (网点×日) 行数 = {totalAfter}（应 = 1）");
            Assert.Equal(1, totalAfter);

            var keep99After = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId] = @org AND [F业务日期] = @d AND [F网点编码] = @code AND [F未及时出仓量] = 99",
                ("@org", OrgId), ("@d", BizDate), ("@code", NetCode));
            Assert.Equal(1, keep99After);
        }
        finally
        {
            await CleanupMetricAsync(conn);
            await CleanupBatchAsync(conn, StgTable, batchId);
            await ExecAsync(conn, $"DELETE FROM [{StgTable}] WHERE [FOrgId] = @org AND [F网点编码] = @code", ("@org", OrgId), ("@code", NetCode));
            if (seededNetwork) await ExecAsync(conn, "DELETE FROM [EXP快递网点] WHERE [F编号] = @code AND [F组织ID] = @org", ("@code", NetCode), ("@org", OrgId));
        }
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
            FBatchNo = $"TESTC2-{Guid.NewGuid():N}",
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
