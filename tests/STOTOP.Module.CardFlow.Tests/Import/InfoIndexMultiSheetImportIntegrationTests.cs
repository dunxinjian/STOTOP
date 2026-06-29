using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.AutoPlugin.Implementations;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Import;
using STOTOP.Module.CardFlow.Services.Import.TransformEngine;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.OA.Entities;
using STOTOP.Module.System.Entities;
using STOTOP.WebAPI.Data.Seeders;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Import;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// Task B18：多 sheet「坑」源 物流信息指数 的真库集成测试（连开发测试库 stotop）。
/// 源文件 excel（物流信息指数）.xls（真 OLE2）含 3 个 sheet：及时性汇总（按天）/完整性汇总（按天）/准确性汇总（按天）。
/// 一个文件、3 个规则（3119/3120/3121）各触发一次 ExcelInputPlugin.ExecuteAsync（PluginRuleId 不同），
/// 验证规则 JSON 里的 sheetName 在完整导入链路真生效——各 sheet 各自落到对应表的对应行数：
///   及时汇总 1 行、完整汇总 2 行、准确汇总 1 行（行数由 python 实读确认）。
/// 并校验完整汇总表含列 F揽收缺失量、准确汇总表含 F到件不准确率 有值（证明对的 sheet 落对的表）。
/// 用完按 F批次ID 清理本测试三批数据，避免污染累积。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop，避免并行跨批去重竞态
public class InfoIndexMultiSheetImportIntegrationTests
{
    private const long OrgId = 192;
    private const string 物流信息指数Xls =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据展示页\excel（物流信息指数）.xls";

    private const string TimelyTable = "STG申通_物流信息及时汇总";
    private const string CompleteTable = "STG申通_物流信息完整汇总";
    private const string AccurateTable = "STG申通_物流信息准确汇总";

    private static STOTOPDbContext CreateDbContext(string conn)
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(CfCard).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(OaExpenseRequest).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(SysUser).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(FinVoucher).Assembly);

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

    [SkippableFact]
    public async Task Migrate_CreatesThreeTables_And_MultiSheetImport_RoutesEachSheetToOwnTable()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接（未设 STOTOP_TEST_CONNECTION 且主树 db-connections.json 不可读），跳过集成测试");
        Skip.IfNot(File.Exists(物流信息指数Xls), $"测试数据文件不存在: {物流信息指数Xls}");

        var conn = TestSqlConnection.GetConnectionString()!;
        // 确保插件内部 DbConnectionsHelper（读 BaseDirectory）也解析到同一 stotop 库
        TestSqlConnection.EnsureSystemConnectionFile();

        await using var db = CreateDbContext(conn);

        // 可达性兜底：连不上则跳过而非失败
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }

        // ── 1. 迁移（幂等建 3 表 + 种子规则 3119/3120/3121 等） ──
        CardFlowSeeder.Migrate(db);

        foreach (var t in new[] { TimelyTable, CompleteTable, AccurateTable })
        {
            var exists = await db.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = N'{t}'")
                .SingleAsync();
            Assert.True(exists > 0, $"Migrate 后应存在表 [{t}]");
        }

        foreach (var rid in new[] { 3119, 3120, 3121 })
        {
            var ruleExists = await db.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS [Value] FROM [CF自动插件_规则] WHERE [FID] = {rid}")
                .SingleAsync();
            Assert.True(ruleExists > 0, $"Migrate 后应存在 CF自动插件_规则 FID={rid}");
        }

        // ── 2. 同一文件、3 个规则各触发一次（sheetName 经规则 JSON 路由各 sheet 到对应表） ──
        long bTimely = 0, bComplete = 0, bAccurate = 0;
        try
        {
            (bTimely, var r1) = await ImportOnceAsync(db, conn, flowDefinitionId: 2319, stageId: 5119, ruleId: 3119);
            Assert.True(r1.Success, $"及时汇总导入应成功，实际 Message={r1.Message}");

            (bComplete, var r2) = await ImportOnceAsync(db, conn, flowDefinitionId: 2320, stageId: 5120, ruleId: 3120);
            Assert.True(r2.Success, $"完整汇总导入应成功，实际 Message={r2.Message}");

            (bAccurate, var r3) = await ImportOnceAsync(db, conn, flowDefinitionId: 2321, stageId: 5121, ruleId: 3121);
            Assert.True(r3.Success, $"准确汇总导入应成功，实际 Message={r3.Message}");

            // ── 3. 各 sheet 行数（python 实读：及时 1 行、完整 2 行、准确 1 行） ──
            var timelyRows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{TimelyTable}] WHERE [F批次ID] = @b", bTimely);
            Assert.Equal(1, timelyRows);

            var completeRows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{CompleteTable}] WHERE [F批次ID] = @b", bComplete);
            Assert.Equal(2, completeRows);

            var accurateRows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{AccurateTable}] WHERE [F批次ID] = @b", bAccurate);
            Assert.Equal(1, accurateRows);

            // ── 4. 对的 sheet 落对的表：完整表 F揽收缺失量 有值、准确表 F到件不准确率 有值 ──
            var completeHasCol = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{CompleteTable}] WHERE [F批次ID] = @b AND [F揽收缺失量] IS NOT NULL", bComplete);
            Assert.Equal(2, completeHasCol);

            var accurateHasCol = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{AccurateTable}] WHERE [F批次ID] = @b AND [F到件不准确率] IS NOT NULL", bAccurate);
            Assert.Equal(1, accurateHasCol);
        }
        finally
        {
            await CleanupBatchAsync(conn, TimelyTable, bTimely);
            await CleanupBatchAsync(conn, CompleteTable, bComplete);
            await CleanupBatchAsync(conn, AccurateTable, bAccurate);
        }
    }

    /// <summary>
    /// 走真实导入链路一次：新建指向物流信息指数文件的 CfBatch + ExcelInputPlugin.ExecuteAsync。
    /// 用不同的 flowDefinitionId / stageId / ruleId 模拟「同一文件、不同规则显式触发」。
    /// </summary>
    private async Task<(long batchId, PluginResult result)> ImportOnceAsync(
        STOTOPDbContext db, string conn, long flowDefinitionId, long stageId, long ruleId)
    {
        var batch = new CfBatch
        {
            FFlowDefinitionId = flowDefinitionId,
            FOrgId = OrgId,
            FTriggeredById = 1,
            FTriggeredTime = DateTime.Now,
            FTriggerType = "fileUpload",
            FFilePath = 物流信息指数Xls,
            FFileName = "excel（物流信息指数）.xls",
            FBatchNo = $"TESTB18-{Guid.NewGuid():N}",
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

    private async Task CleanupBatchAsync(string conn, string table, long batchId)
    {
        if (batchId <= 0) return;
        try
        {
            await using var c = new SqlConnection(conn);
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"DELETE FROM [{table}] WHERE [F批次ID] = @b";
            cmd.Parameters.AddWithValue("@b", batchId);
            await cmd.ExecuteNonQueryAsync();
        }
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

    private static async Task<int> CountAsync(string conn, string sql, long batchId)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@b", batchId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
