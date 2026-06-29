using System.Threading.Channels;
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
/// Plan2 Phase C — C0：归一服务「事件类（物流完整性明细 → 质量事件）」worked example 集成测试（连开发测试库 stotop）。
///
/// 安排：EnsureTables 建 QL 表 + CardFlowSeeder.Migrate 建 STG 表；种子 ExpNetworkPoint(FCode=320288,
///   FFullName=江苏太仓市城区公司, FOrgId=192)；用 Plan 1 导入封装把 excel (未到件).xls（15 行）导入
///   STG申通_物流完整性明细(orgId=192)。
/// 行为：svc.UnifyShentongAsync(192)。
/// 断言：QL申通_承运商质量事件 出现 15 行（本源），F质量域=物流信息、F问题类型名称=未到件、
///   F网点编码=320288（名称匹配成功，F网点匹配状态=1）、F来源STG表/F来源行ID 正确；
///   QL申通_质量问题字典 自建「未到件」条目；再跑一次 → 事件行数仍 15（幂等）。
/// 清理：try/finally 删本测试 orgId 的 QL 事件/字典 + STG 行 + CfBatch + 种子网点。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class UnifyLogisticsCompletenessTests
{
    private const long OrgId = 192;
    private const string NetCode = "320288";
    private const string NetFullName = "江苏太仓市城区公司";
    private const string EventTable = "QL申通_承运商质量事件";
    private const string DictTable = "QL申通_质量问题字典";
    private const string StgTable = "STG申通_物流完整性明细";

    private static readonly string 明细Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细";

    private readonly ITestOutputHelper _log;
    public UnifyLogisticsCompletenessTests(ITestOutputHelper log) => _log = log;

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
    public async Task Unify_LogisticsCompleteness_ProducesEvents_MatchesNetwork_AutoCreatesDict_Idempotent()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        var path = global::System.IO.Path.Combine(明细Dir, "excel (未到件).xls");
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
            // 预清：删除可能残留的本 org 事件/字典（保证可复现）
            await CleanupQualityAsync(conn);

            // 种子网点主数据（名称匹配的目标）
            seededNetwork = await EnsureNetworkAsync(db);

            // ── 导入 STG（15 行）──
            (batchId, var importResult) = await ImportOnceAsync(db, conn, path, 2301, 5101, 3101);
            Assert.True(importResult.Success, $"导入应成功，实际 Message={importResult.Message}");
            var stgRows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{StgTable}] WHERE [F批次ID] = @b", ("@b", batchId));
            Assert.Equal(15, stgRows);

            // ── 行为：归一 ──
            var svc = new QualityUnificationService(db, new MasterDataMatcher(db));
            var result = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[首跑] EventsUpserted={result.EventsUpserted} NetUnmatched={result.NetworkUnmatched} EmpUnmatched={result.EmployeeUnmatched}");
            Assert.Equal(15, result.EventsUpserted);

            // ── 断言：事件 15 行（本源本批）──
            var eventRows = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId));
            _log.WriteLine($"[事件] 本批事件行数 = {eventRows}（期望 15）");
            Assert.Equal(15, eventRows);

            // 质量域 = 物流信息
            var wrongDomain = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源批次ID] = @b AND [F质量域] <> N'物流信息'",
                ("@org", OrgId), ("@b", batchId));
            Assert.Equal(0, wrongDomain);

            // 问题类型名称 = 未到件
            var wrongProblem = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源批次ID] = @b AND ([F问题类型名称] IS NULL OR [F问题类型名称] <> N'未到件')",
                ("@org", OrgId), ("@b", batchId));
            Assert.Equal(0, wrongProblem);

            // 网点编码 = 320288 且匹配状态 = 1（名称匹配成功）
            var netOk = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源批次ID] = @b AND [F网点编码] = @code AND [F网点匹配状态] = 1",
                ("@org", OrgId), ("@b", batchId), ("@code", NetCode));
            _log.WriteLine($"[网点] 编码={NetCode}/状态=1 行数 = {netOk}（期望 15）");
            Assert.Equal(15, netOk);

            // 来源行ID 全部对应到本批 STG FID（非 0、能反查 STG）
            var orphanRowId = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{EventTable}] e WHERE e.[FOrgId] = @org AND e.[F来源批次ID] = @b
                   AND NOT EXISTS (SELECT 1 FROM [{StgTable}] s WHERE s.[FID] = e.[F来源行ID] AND s.[F批次ID] = @b)",
                ("@org", OrgId), ("@b", batchId));
            Assert.Equal(0, orphanRowId);

            // ── 字典自建「未到件」──
            var dictRows = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{DictTable}] WHERE [FOrgId] = @org AND [F承运商] = N'申通' AND [F质量域] = N'物流信息' AND [F来源问题类型原文] = N'未到件'",
                ("@org", OrgId));
            _log.WriteLine($"[字典] 未到件条目数 = {dictRows}（期望 1）");
            Assert.Equal(1, dictRows);

            // ── 幂等：再跑一次 → 事件仍 15、字典仍 1 ──
            var result2 = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[复跑] EventsUpserted={result2.EventsUpserted}");
            Assert.Equal(15, result2.EventsUpserted);

            var eventRows2 = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId));
            _log.WriteLine($"[幂等] 复跑后事件行数 = {eventRows2}（期望 15）");
            Assert.Equal(15, eventRows2);

            var dictRows2 = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{DictTable}] WHERE [FOrgId] = @org AND [F质量域] = N'物流信息' AND [F来源问题类型原文] = N'未到件'",
                ("@org", OrgId));
            Assert.Equal(1, dictRows2);
        }
        finally
        {
            await CleanupQualityAsync(conn);
            await CleanupBatchAsync(conn, StgTable, batchId);
            if (seededNetwork) await CleanupNetworkAsync(conn);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 安排 / 清理 helper
    // ─────────────────────────────────────────────────────────────

    /// <summary>种子网点（若已存在同 code 则不重复种，返回是否由本测试新种）。</summary>
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
            FBatchNo = $"TESTC0-{Guid.NewGuid():N}",
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

    /// <summary>删本测试 org 的全部归一产物（事件 + 字典），保证预清/收尾干净。</summary>
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
