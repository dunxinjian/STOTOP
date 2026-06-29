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
/// Plan2 Phase C — C3 地基：通用事件归一路径（UnifyGenericEventAsync，列名来自描述符 + StgRawReader raw-SQL 读）
/// 的「worked example」集成测试（连开发测试库 stotop）。第一个走通用路径的事件源：物流及时准确明细。
///
/// 安排：建 STG + QL 表；种子 ExpNetworkPoint(320288=江苏太仓市城区公司, org=192)；
///   用 Plan1 导入封装把「excel (到件晚于签收).xls」导入 STG申通_物流及时准确明细(org=192)。
/// 行为：svc.UnifyShentongAsync(192) —— 物流完整性走 C0 typed、本源走通用事件路径，互不干扰。
/// 断言：QL申通_承运商质量事件 中 F来源STG表='STG申通_物流及时准确明细' 行数 > 0 且 == 本批 STG 行数（全部归一）；
///   F质量域=物流信息；F问题类型名称 与 STG distinct(F问题类型) 一致（正确取自描述符问题类型列）；
///   网点 320288 匹配(状态1)；F关键字段JSON 全非空；再跑一次行数不变（幂等）。
/// 清理：try/finally 删本 org 的 QL 事件/字典 + STG 行 + CfBatch + 种子网点。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class UnifyGenericEventTests
{
    private const long OrgId = 192;
    private const string NetCode = "320288";
    private const string NetFullName = "江苏太仓市城区公司";
    private const string EventTable = "QL申通_承运商质量事件";
    private const string DictTable = "QL申通_质量问题字典";
    private const string StgTable = "STG申通_物流及时准确明细";

    private static readonly string 明细Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细";

    private readonly ITestOutputHelper _log;
    public UnifyGenericEventTests(ITestOutputHelper log) => _log = log;

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
        var dir = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "stqc_unify_gen_snap");
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
    public async Task Unify_GenericEvent_LogisticsTimeliness_ProducesEvents_MatchesNetwork_AutoCreatesDict_Idempotent()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        // 物流及时准确明细：三个源文件之一（靠 F问题类型 区分）；用「到件晚于签收」作样例。
        var path = global::System.IO.Path.Combine(明细Dir, "excel (到件晚于签收).xls");
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

            // ── 导入 STG（物流及时准确明细，flow 2302 / stage 5102 / rule 3102）──
            (batchId, var importResult) = await ImportOnceAsync(db, conn, path, 2302, 5102, 3102);
            Assert.True(importResult.Success, $"导入应成功，实际 Message={importResult.Message}");

            var stgRows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{StgTable}] WHERE [F批次ID] = @b", ("@b", batchId));
            _log.WriteLine($"[STG] 导入行数 = {stgRows}");
            Assert.True(stgRows > 0, "样例应至少导入 1 行 STG");

            // STG 里的 distinct 问题类型集合（用于核对事件问题类型名称「正确」，不硬编码字面值）
            var stgProblems = await DistinctProblemTypesAsync(conn, batchId);
            _log.WriteLine($"[STG] distinct F问题类型 = [{string.Join(",", stgProblems)}]");
            Assert.NotEmpty(stgProblems);

            // ── 行为：归一（物流完整性走 C0 typed、本源走通用事件路径，互不干扰）──
            var svc = new QualityUnificationService(db, new MasterDataMatcher(db));
            var result = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[首跑] EventsUpserted={result.EventsUpserted} NetUnmatched={result.NetworkUnmatched} EmpUnmatched={result.EmployeeUnmatched}");

            // ── 断言：本源（物流及时准确明细）事件行数 == 本批 STG 行数 且 > 0 ──
            var eventRows = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId));
            _log.WriteLine($"[事件] 本源本批事件行数 = {eventRows}（期望 {stgRows}）");
            Assert.True(eventRows > 0, "通用事件路径应产出事件");
            Assert.Equal(stgRows, eventRows);

            // 质量域 = 物流信息
            var wrongDomain = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND [F质量域] <> N'物流信息'",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId));
            Assert.Equal(0, wrongDomain);

            // 问题类型名称「正确」：事件中出现的 distinct 问题类型名称集合 == STG distinct(F问题类型)
            var evtProblems = await DistinctEventProblemNamesAsync(conn, batchId);
            _log.WriteLine($"[事件] distinct F问题类型名称 = [{string.Join(",", evtProblems)}]");
            Assert.Equal(stgProblems.OrderBy(x => x), evtProblems.OrderBy(x => x));

            // 网点 320288 匹配（状态1）：本源网点仅有名称，名称匹配到种子网点 → 全部状态1/编码320288
            var netOk = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND [F网点编码] = @code AND [F网点匹配状态] = 1",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId), ("@code", NetCode));
            _log.WriteLine($"[网点] 编码={NetCode}/状态=1 行数 = {netOk}（期望 {eventRows}）");
            Assert.Equal(eventRows, netOk);

            // 关键字段 JSON 全非空
            var emptyJson = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b AND ([F关键字段JSON] IS NULL OR LTRIM(RTRIM([F关键字段JSON])) = N'')",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId));
            Assert.Equal(0, emptyJson);

            // 来源行ID 全部能反查本批 STG（非孤儿）
            var orphanRowId = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{EventTable}] e WHERE e.[FOrgId] = @org AND e.[F来源STG表] = @tbl AND e.[F来源批次ID] = @b
                   AND NOT EXISTS (SELECT 1 FROM [{StgTable}] s WHERE s.[FID] = e.[F来源行ID] AND s.[F批次ID] = @b)",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId));
            Assert.Equal(0, orphanRowId);

            // 字典：本源问题类型在「物流信息」域下均已自建
            foreach (var pt in stgProblems)
            {
                var dictRows = await CountAsync(conn,
                    $"SELECT COUNT(*) FROM [{DictTable}] WHERE [FOrgId] = @org AND [F承运商] = N'申通' AND [F质量域] = N'物流信息' AND [F来源问题类型原文] = @pt",
                    ("@org", OrgId), ("@pt", pt));
                Assert.True(dictRows == 1, $"问题类型「{pt}」应自建字典 1 条，实际 {dictRows}");
            }

            // ── 幂等：再跑一次 → 本源事件行数不变 ──
            var result2 = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[复跑] EventsUpserted={result2.EventsUpserted}");
            var eventRows2 = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId] = @org AND [F来源STG表] = @tbl AND [F来源批次ID] = @b",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId));
            _log.WriteLine($"[幂等] 复跑后本源事件行数 = {eventRows2}（期望 {eventRows}）");
            Assert.Equal(eventRows, eventRows2);
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
            FBatchNo = $"TESTC3-{Guid.NewGuid():N}",
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

    /// <summary>本批 STG distinct 非空 F问题类型。</summary>
    private static async Task<List<string>> DistinctProblemTypesAsync(string conn, long batchId)
    {
        var list = new List<string>();
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT DISTINCT [F问题类型] FROM [{StgTable}] WHERE [F批次ID] = @b AND [F问题类型] IS NOT NULL AND LTRIM(RTRIM([F问题类型])) <> N''";
        cmd.Parameters.AddWithValue("@b", batchId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetString(0).Trim());
        return list;
    }

    /// <summary>本源本批事件 distinct 非空 F问题类型名称。</summary>
    private static async Task<List<string>> DistinctEventProblemNamesAsync(string conn, long batchId)
    {
        var list = new List<string>();
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT DISTINCT [F问题类型名称] FROM [{EventTable}] WHERE [F来源STG表] = N'{StgTable}' AND [F来源批次ID] = @b AND [F问题类型名称] IS NOT NULL";
        cmd.Parameters.AddWithValue("@b", batchId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetString(0).Trim());
        return list;
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
