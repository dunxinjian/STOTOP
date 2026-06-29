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
/// Plan2 D2：「固定常量问题类型」字典种子（<see cref="QualityUnifySeeder.SeedConstantProblemDict"/>）集成测试（连开发测试库 stotop）。
///
/// 种子预置 8 条「源代码里写死的 ProblemTypeConstant」常量类型（给可读英文编码 + 合理默认），
/// 域 + 来源问题类型原文 与 <see cref="ShentongSourceMap"/> 各源 QualityDomain/ProblemTypeConstant 逐字一致，
/// 故归一 <see cref="QualityUnificationService"/> 会命中种子（用种子编码），不再用前缀_短哈希自建这批高频类型。
///
/// 三类断言：
///  ① 幂等：跑种子两次，第二次不新增、不报错（行数仍 8）。
///  ② 8 条按 (org192,申通,域,原文) 存在，且 编码/严重度/是否考核/是否可归责到人 = 预期。
///  ③ 命中归一（[SkippableFact]，样例文件缺则跳过）：导入「签收未达标」常量源 → UnifyShentongAsync(192)
///     → 断言该域该原文的事件用<b>种子编码 SIGN_SUBSTD</b>（命中种子，非自建哈希码 SIGN_xxxxxxxx）。
/// 清理：arrange 预清本 org 这 8 条种子残留 + try/finally 自清（命中归一测试另清事件/STG/批次/网点）。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class ProblemDictSeedTests
{
    private const long OrgId = 192;
    private const string DictTable = "QL申通_质量问题字典";
    private const string EventTable = "QL申通_承运商质量事件";
    private const string NetCode = "320288";
    private const string NetFullName = "江苏太仓市城区公司";

    private static readonly string 明细Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细";

    /// <summary>8 条常量种子的期望值（域/原文/编码/严重度/是否考核/是否可归责），与 SeedConstantProblemDict 对齐。</summary>
    private static readonly (string Domain, string Raw, string Code, int Severity, bool Assessed, bool Attributable)[] Expected =
    {
        ("揽收时效",     "揽收不及时",  "PICK_LATE",            1, false, true),
        ("出仓时效",     "未及时出仓",  "OUTB_LATE",            1, false, true),
        ("交货滞留",     "交货滞留",    "HANDOVER_DELAY",       1, false, true),
        ("派送签收时效", "签收未达标",  "SIGN_SUBSTD",          1, false, true),
        ("积压与遗失",   "疑似遗失",    "SUSPECT_LOSS",         2, false, true),
        ("虚假签收履约", "虚签投诉",    "FAKE_SIGN_COMPLAINT",  2, false, true),
        ("虚假签收履约", "虚假签收",    "FAKE_SIGN",            2, false, true),
        ("虚假签收履约", "履约失败",    "FULFILL_FAIL",         1, false, true),
    };

    private readonly ITestOutputHelper _log;
    public ProblemDictSeedTests(ITestOutputHelper log) => _log = log;

    // ─────────────────────────────────────────────────────────────
    // ① 幂等 + ② 8 条按键存在且字段=预期
    // ─────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task SeedConstantProblemDict_IsIdempotent_And_EightConstantsExistWithExpectedValues()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        await using var db = CreateDbContext(conn);
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }

        // 建表（EnsureTables 也会调一次种子，无妨——幂等）。
        QualityUnifySeeder.EnsureTables(db);

        try
        {
            // 预清：删本 org 这 8 条种子原文残留（精确按四元键，不动其它字典条目）。
            await DeleteSeedRowsAsync(conn);

            // ── ① 幂等：跑种子两次 ──
            QualityUnifySeeder.SeedConstantProblemDict(db, OrgId);
            var countAfter1 = await CountSeedRowsAsync(conn);
            _log.WriteLine($"[首跑] 8 条常量种子存在数 = {countAfter1}（期望 8）");
            Assert.Equal(Expected.Length, countAfter1);

            // 第二次：不新增、不报错。
            QualityUnifySeeder.SeedConstantProblemDict(db, OrgId);
            var countAfter2 = await CountSeedRowsAsync(conn);
            _log.WriteLine($"[复跑] 8 条常量种子存在数 = {countAfter2}（期望仍 8，幂等）");
            Assert.Equal(Expected.Length, countAfter2);

            // ── ② 逐条断言按 (org192,申通,域,原文) 存在，且 编码/严重度/考核/可归责 = 预期 ──
            foreach (var e in Expected)
            {
                var row = await GetSeedRowAsync(conn, e.Domain, e.Raw);
                Assert.True(row != null, $"种子缺失：域={e.Domain} 原文={e.Raw}");
                Assert.Equal(e.Code, row!.Value.Code);
                Assert.Equal(e.Raw, row.Value.Name); // 名称=原文
                Assert.Equal(e.Severity, row.Value.Severity);
                Assert.Equal(e.Assessed, row.Value.Assessed);
                Assert.Equal(e.Attributable, row.Value.Attributable);
                Assert.Equal(1, row.Value.Status);
                _log.WriteLine($"[OK] {e.Domain}/{e.Raw} → 编码={row.Value.Code} 严重度={row.Value.Severity} 考核={row.Value.Assessed} 可归责={row.Value.Attributable}");
            }
        }
        finally
        {
            await DeleteSeedRowsAsync(conn);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // ③ 命中归一：常量源事件用种子编码（非自建哈希码）
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 导入「签收未达标」常量源（域=派送签收时效，常量「签收未达标」）→ 归一 → 事件 F问题类型编码 应 = 种子码 SIGN_SUBSTD。
    /// 关键：本测试<b>不删字典</b>（种子需在归一前就位），归一查到种子即用、命中种子码而非自建 SIGN_xxxxxxxx。
    /// </summary>
    [SkippableFact]
    public async Task Unify_ConstantSource_HitsSeededDict_UsesSeedCode_NotHash()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        // 签收未达标样例（与 ShentongBatch1EventSourcesTests 同文件 / flow / stage / rule）。
        const string sampleFile = "ossf0ba7cd6c1524ab8898e157103219f0f签收未达标.xlsx";
        const string stgTable = "STG申通_签收未达标明细";
        const string domain = "派送签收时效";
        const string raw = "签收未达标";
        const string seedCode = "SIGN_SUBSTD";
        long flow = 2307, stage = 5107, rule = 3107;

        var path = global::System.IO.Path.Combine(明细Dir, sampleFile);
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
            // 预清：本 org 事件 + 本源 STG 本网点残留（不删字典——种子要保留）；确保种子在位。
            await ExecAsync(conn, $"DELETE FROM [{EventTable}] WHERE [FOrgId] = @org", ("@org", OrgId));
            await ExecAsync(conn, $"DELETE FROM [{stgTable}] WHERE [FOrgId]=@org AND [F应签网点]=@name", ("@org", OrgId), ("@name", NetFullName));
            await DeleteSeedRowsAsync(conn);
            QualityUnifySeeder.SeedConstantProblemDict(db, OrgId); // 确保 8 条种子在位（含 SIGN_SUBSTD）

            seededNetwork = await EnsureNetworkAsync(db);

            (batchId, var importResult) = await ImportOnceAsync(db, conn, path, flow, stage, rule);
            Assert.True(importResult.Success, $"导入应成功，实际 Message={importResult.Message}");
            var stgRows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{stgTable}] WHERE [F批次ID] = @b", ("@b", batchId));
            Assert.True(stgRows > 0, "样例应至少导入 1 行 STG");

            // 归一。
            var svc = new QualityUnificationService(db, new MasterDataMatcher(db));
            var result = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[归一] EventsUpserted={result.EventsUpserted}");

            // ── 断言：本源本批事件全部用种子编码 SIGN_SUBSTD（命中种子，非自建哈希）──
            var eventRows = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@tbl AND [F来源批次ID]=@b",
                ("@org", OrgId), ("@tbl", stgTable), ("@b", batchId));
            var seedCodeRows = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@tbl AND [F来源批次ID]=@b AND [F质量域]=@dom AND [F问题类型编码]=@code",
                ("@org", OrgId), ("@tbl", stgTable), ("@b", batchId), ("@dom", domain), ("@code", seedCode));
            _log.WriteLine($"[命中种子] 事件总数={eventRows} 用种子码 {seedCode} 的行数={seedCodeRows}（期望相等）");
            Assert.True(eventRows > 0, "应产出事件");
            Assert.Equal(eventRows, seedCodeRows);

            // ── 断言：字典里该 (域,原文) 仍只有种子那一条（未被归一自建出第二条哈希条目）──
            var dictCount = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{DictTable}] WHERE [FOrgId]=@org AND [F承运商]=N'申通' AND [F质量域]=@dom AND [F来源问题类型原文]=@raw",
                ("@org", OrgId), ("@dom", domain), ("@raw", raw));
            Assert.Equal(1, dictCount);
            var dictCode = await ScalarStringAsync(conn,
                $"SELECT [F问题类型编码] FROM [{DictTable}] WHERE [FOrgId]=@org AND [F承运商]=N'申通' AND [F质量域]=@dom AND [F来源问题类型原文]=@raw",
                ("@org", OrgId), ("@dom", domain), ("@raw", raw));
            Assert.Equal(seedCode, dictCode);
        }
        finally
        {
            await ExecAsync(conn, $"DELETE FROM [{EventTable}] WHERE [FOrgId] = @org", ("@org", OrgId));
            await DeleteSeedRowsAsync(conn);
            await CleanupBatchAsync(conn, stgTable, batchId);
            await ExecAsync(conn, $"DELETE FROM [{stgTable}] WHERE [FOrgId]=@org AND [F应签网点]=@name", ("@org", OrgId), ("@name", NetFullName));
            if (seededNetwork) await ExecAsync(conn, "DELETE FROM [EXP快递网点] WHERE [F编号] = @code AND [F组织ID] = @org", ("@code", NetCode), ("@org", OrgId));
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 种子查询 helper
    // ─────────────────────────────────────────────────────────────

    private static async Task<int> CountSeedRowsAsync(string conn)
    {
        int n = 0;
        foreach (var e in Expected)
        {
            var c = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{DictTable}] WHERE [FOrgId]=@org AND [F承运商]=N'申通' AND [F质量域]=@dom AND [F来源问题类型原文]=@raw",
                ("@org", OrgId), ("@dom", e.Domain), ("@raw", e.Raw));
            n += c;
        }
        return n;
    }

    private static async Task DeleteSeedRowsAsync(string conn)
    {
        foreach (var e in Expected)
        {
            await ExecAsync(conn,
                $"DELETE FROM [{DictTable}] WHERE [FOrgId]=@org AND [F承运商]=N'申通' AND [F质量域]=@dom AND [F来源问题类型原文]=@raw",
                ("@org", OrgId), ("@dom", e.Domain), ("@raw", e.Raw));
        }
    }

    private static async Task<(string Code, string Name, int Severity, bool Assessed, bool Attributable, int Status)?> GetSeedRowAsync(
        string conn, string domain, string raw)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText =
            $@"SELECT [F问题类型编码],[F问题类型名称],[F默认严重度],[F是否考核],[F是否可归责到人],[F状态]
               FROM [{DictTable}]
               WHERE [FOrgId]=@org AND [F承运商]=N'申通' AND [F质量域]=@dom AND [F来源问题类型原文]=@raw";
        cmd.Parameters.AddWithValue("@org", OrgId);
        cmd.Parameters.AddWithValue("@dom", domain);
        cmd.Parameters.AddWithValue("@raw", raw);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return (
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetBoolean(3),
            reader.GetBoolean(4),
            reader.GetInt32(5));
    }

    // ─────────────────────────────────────────────────────────────
    // Arrange / 通用 helper（仿 ShentongBatch1EventSourcesTests）
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

    private static string Snapshot(string srcPath)
    {
        var dir = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "stqc_dictseed_snap");
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
            FBatchNo = $"TESTD2-{Guid.NewGuid():N}",
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

    private static async Task<string?> ScalarStringAsync(string conn, string sql, params (string name, object val)[] ps)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, val) in ps) cmd.Parameters.AddWithValue(name, val);
        var o = await cmd.ExecuteScalarAsync();
        return o is null or DBNull ? null : o.ToString();
    }
}
