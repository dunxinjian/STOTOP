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
/// Plan2 Phase C — C3 批次5：归一服务「网点指标类 4 个汇总源（最后一批源）→ 网点日质量指标（各填互不相交字段子集，按 网点×日 合并 upsert）」集成测试（连开发测试库 stotop）。
/// 仿 <see cref="ShentongBatch4NetworkMetricTests"/>。4 源：
///   末端派送网点汇总（无网点编码列，按名称匹配；签收域子集：一/二阶段/当天及时签收率 + 派送预估考核金额/有偿派费/预计返款）；
///   签收率考核汇总（有网点编码；签收域子集：仅 F签收率考核金额）；
///   拦截汇总（无网点编码列，按名称匹配；拦截子集：应拦截量/拦截成功率/及时转出率）；
///   渗透建站考核（有网点编码；渗透子集：自建渗透率/渗透率目标/建站待完成/喵柜激活格口数；日期列 F统计周期 = 「YYYY-第MM月」→ 月首日）。
///
/// 4 源样例都属网点 江苏太仓市城区公司(320288)；真实日期（来自样例文件实读）：
///   末端派送=2026-06-16、拦截=2026-06-15、签收率=2026-06-15、渗透周期「2026-第06月」→ 月首日 2026-06-01。
/// 故归一后会形成 3 个 (网点×日) 网点日指标行：
///   2026-06-16 — 末端派送（签收率/派送考核金额）；
///   2026-06-15 — 拦截（应拦截量/拦截成功率/及时转出率）+ 签收率考核（F签收率考核金额）两源合并；
///   2026-06-01 — 渗透（自建渗透率/渗透率目标/建站待完成/喵柜激活格口数）。
///
/// 断言：
///   ① 每源本源字段子集落值非空；
///   ② 按 (网点×日) 建/更行（3 行）；
///   ③ 签收域不抢字段回归：预置一条 2026-06-16 同网点行手填 F签收率考核金额=77.77（属签收率考核汇总源，但本批无签收率源落 6-16）→
///      跑后该行既被末端派送填上 一阶段及时签收率(来自Delivery)，又保留 F签收率考核金额=77.77（互不清空）；
///      并核对 6-15 行确有 F签收率考核金额（来自 SignRate）且 6-15 行无签收率字段、6-16 行无 F应拦截量（子集互不相交）；
///   ④ 幂等：再跑一次行数仍 3、子集值不变、哨兵仍在。
/// 清理：try/finally 删本 org 网点日指标 + 各 STG 行（按网点编码/名称）+ CfBatch + 种子网点。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class ShentongBatch5NetworkMetricTests
{
    private const long OrgId = 192;
    private const string NetCode = "320288";
    private const string NetFullName = "江苏太仓市城区公司";
    private const string MetricTable = "QL申通_网点日质量指标";

    private static readonly DateTime Date16 = new(2026, 6, 16); // 末端派送
    private static readonly DateTime Date15 = new(2026, 6, 15); // 拦截 + 签收率考核
    private static readonly DateTime Date01 = new(2026, 6, 1);  // 渗透（周期 2026-第06月 → 月首日）

    private static readonly string 展示页Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据展示页";
    private static readonly string 明细Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细";

    // 文件名（展示页/明细），flow/stage/rule 来自 CardFlowSeeder（见 ShentongQualityBatchE2ETests）。
    private const string DeliveryFile = "末端派送考核(新)网点汇总V2-32e3bffc4d38439292255ddacd9cc6ab.xlsx";
    private const string SignRateFile = "ossdfa26db73e7f4f548c21e65749ea6d65（签收率未达标考核）.xlsx";
    private const string InterceptFile = "网点数据报表导出__20260617163734_ecd21f0df83c4daa871f05f2ab6ddd76.xls"; // 扩展名 .xls 实为 xlsx
    private const string PenetrationFile = "渗透率建站考核_导出任务_20260617_109362809.xlsx";

    private const string DeliveryTable = "STG申通_末端派送网点汇总";
    private const string SignRateTable = "STG申通_签收率考核汇总";
    private const string InterceptTable = "STG申通_拦截汇总";
    private const string PenetrationTable = "STG申通_渗透建站考核";

    private readonly ITestOutputHelper _log;
    public ShentongBatch5NetworkMetricTests(ITestOutputHelper log) => _log = log;

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
        var dir = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "stqc_unify_b5_snap");
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
    public async Task Unify_Batch5_FourNetworkMetricSources_SubsetsFilled_SignDomainNoSteal_Idempotent()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        var deliveryPath = global::System.IO.Path.Combine(展示页Dir, DeliveryFile);
        var signRatePath = global::System.IO.Path.Combine(展示页Dir, SignRateFile);
        var interceptPath = global::System.IO.Path.Combine(展示页Dir, InterceptFile);
        var penetrationPath = global::System.IO.Path.Combine(明细Dir, PenetrationFile);
        Skip.IfNot(File.Exists(deliveryPath), $"样例文件不存在: {deliveryPath}");
        Skip.IfNot(File.Exists(signRatePath), $"样例文件不存在: {signRatePath}");
        Skip.IfNot(File.Exists(interceptPath), $"样例文件不存在: {interceptPath}");
        Skip.IfNot(File.Exists(penetrationPath), $"样例文件不存在: {penetrationPath}");

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

            // ── 签收域不抢字段回归：预置一条 2026-06-16 同网点行，手填 F签收率考核金额=77.77 ──
            // F签收率考核金额 属签收率考核汇总源（其样例落 2026-06-15）；本批无任何源在 2026-06-16 写它 →
            // 末端派送汇总在 2026-06-16 落签收率/派送考核金额时绝不能清掉它（不抢字段）。
            await ExecAsync(conn,
                $@"INSERT INTO [{MetricTable}] ([FOrgId],[F承运商],[F业务日期],[F网点编码],[F签收率考核金额],[F创建时间])
                   VALUES (@org, N'申通', @d, @code, 77.77, GETDATE())",
                ("@org", OrgId), ("@d", Date16), ("@code", NetCode));

            // ── 导入 4 个 STG（flow/stage/rule 来自 CardFlowSeeder）──
            //   末端派送 2325/5125/3125、签收率考核 2322/5122/3122、拦截 2328/5128/3128、渗透 2318/5118/3118。
            await ImportAndExpect(db, conn, batchIds, deliveryPath, 2325, 5125, 3125, DeliveryTable, 1);
            await ImportAndExpect(db, conn, batchIds, signRatePath, 2322, 5122, 3122, SignRateTable, 1);
            await ImportAndExpect(db, conn, batchIds, interceptPath, 2328, 5128, 3128, InterceptTable, 1);
            await ImportAndExpect(db, conn, batchIds, penetrationPath, 2318, 5118, 3118, PenetrationTable, 1);

            // ── 行为：归一（泛化分发同时跑事件/员工/网点指标类；本测试断言 4 网点指标源产物）──
            var svc = new QualityUnificationService(db, new MasterDataMatcher(db));
            var result = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[首跑] NetMetricUpserts={result.NetworkMetricUpserts} NetUnmatched={result.NetworkUnmatched}");
            Assert.True(result.NetworkMetricUpserts > 0, $"应有网点指标 upsert，实际 {result.NetworkMetricUpserts}");

            // ── 断言 ①：各源本源字段子集落值非空 ──
            // 末端派送（2026-06-16）：一/二阶段/当天及时签收率 + 派送预估考核金额（样例 F预计考核金额=0.0，非空）。
            //   有偿派费/预计返款样例为空字符串 → TryDecimal=null（正确，无值不造值），故不强断这两列。
            await AssertNotNull(conn, Date16, "F一阶段及时签收率");
            await AssertNotNull(conn, Date16, "F二阶段及时签收率");
            await AssertNotNull(conn, Date16, "F当天及时签收率");
            await AssertNotNull(conn, Date16, "F派送预估考核金额");
            // 签收率考核（2026-06-15）：F签收率考核金额（样例 F总金额=0.0，非空）。
            await AssertNotNull(conn, Date15, "F签收率考核金额");
            // 拦截（2026-06-15）：应拦截量/拦截成功率/及时转出率。
            await AssertNotNull(conn, Date15, "F应拦截量");
            await AssertNotNull(conn, Date15, "F拦截成功率");
            await AssertNotNull(conn, Date15, "F及时转出率");
            // 渗透（2026-06-01 月首日）：自建渗透率/渗透率目标/建站待完成/喵柜激活格口数。
            await AssertNotNull(conn, Date01, "F自建渗透率");
            await AssertNotNull(conn, Date01, "F渗透率目标");
            await AssertNotNull(conn, Date01, "F建站待完成");
            await AssertNotNull(conn, Date01, "F喵柜激活格口数");

            // ── 断言：率单位口径（率存百分数，% 仅去符号不除100）──
            // 拦截成功率源 83.87% → 83.87；渗透 自建渗透率源 11.61% → 11.61。
            var 拦截成功率 = await ScalarDecimalAsync(conn,
                $"SELECT [F拦截成功率] FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code",
                ("@org", OrgId), ("@d", Date15), ("@code", NetCode));
            var 自建渗透率 = await ScalarDecimalAsync(conn,
                $"SELECT [F自建渗透率] FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code",
                ("@org", OrgId), ("@d", Date01), ("@code", NetCode));
            _log.WriteLine($"[率单位] F拦截成功率={拦截成功率}(期望≈83.87) F自建渗透率={自建渗透率}(期望≈11.61)");
            Assert.NotNull(拦截成功率);
            Assert.NotNull(自建渗透率);
            Assert.True(Math.Abs(拦截成功率!.Value - 83.87m) < 0.01m, $"拦截成功率应≈83.87，实际 {拦截成功率}");
            Assert.True(Math.Abs(自建渗透率!.Value - 11.61m) < 0.01m, $"自建渗透率应≈11.61，实际 {自建渗透率}");

            // ── 断言 ②：行数——本批形成恰 3 个 (网点×日) 行（6-16 / 6-15 / 6-01）──
            var totalRows = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F网点编码]=@code",
                ("@org", OrgId), ("@code", NetCode));
            _log.WriteLine($"[行数] (网点×日) 行数={totalRows}（期望3：6-16/6-15/6-01）");
            Assert.Equal(3, totalRows);

            // ── 断言 ③：签收域不抢字段回归 ──
            // 6-16 行：既被末端派送填上 F一阶段及时签收率，又保留预置的 F签收率考核金额=77.77（末端派送不碰它）。
            var noSteal16 = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{MetricTable}]
                   WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code
                     AND [F一阶段及时签收率] IS NOT NULL AND [F签收率考核金额]=77.77",
                ("@org", OrgId), ("@d", Date16), ("@code", NetCode));
            _log.WriteLine($"[签收域不抢字段] 6-16 行 末端派送填签收率 + 保留 F签收率考核金额=77.77 行数={noSteal16}（期望1）");
            Assert.Equal(1, noSteal16);

            // 反向：6-16 行不应被签收率考核汇总写 F签收率考核金额（该源样例在 6-15），故 6-16 的 77.77 是预置哨兵被保留而非源写入；
            // 同时 6-16 行不应出现拦截子集（F应拦截量 应为 NULL——拦截源在 6-15，子集互不相交、不串行落到 6-16）。
            var intercept16 = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code AND [F应拦截量] IS NULL",
                ("@org", OrgId), ("@d", Date16), ("@code", NetCode));
            Assert.Equal(1, intercept16);

            // 6-15 行：签收率考核汇总写 F签收率考核金额（非哨兵——预置哨兵在 6-16），且 6-15 行无末端派送签收率子集（F一阶段及时签收率 NULL）。
            var signOn15 = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{MetricTable}]
                   WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code
                     AND [F签收率考核金额] IS NOT NULL AND [F一阶段及时签收率] IS NULL AND [F应拦截量] IS NOT NULL",
                ("@org", OrgId), ("@d", Date15), ("@code", NetCode));
            _log.WriteLine($"[签收域不抢字段] 6-15 行 签收率考核金额+拦截子集 同行、且无末端派送签收率 行数={signOn15}（期望1，证明两域子集互不相交）");
            Assert.Equal(1, signOn15);

            // ── 断言：渗透月首日口径——6-01 行存在且承载渗透子集（验证「2026-第06月」→ 2026-06-01）──
            var penetrationOn01 = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{MetricTable}]
                   WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code
                     AND [F自建渗透率] IS NOT NULL AND [F喵柜激活格口数] IS NOT NULL",
                ("@org", OrgId), ("@d", Date01), ("@code", NetCode));
            _log.WriteLine($"[渗透月首日] 6-01 行渗透子集非空 行数={penetrationOn01}（期望1，周期 2026-第06月 → 月首日）");
            Assert.Equal(1, penetrationOn01);

            // ── 断言 ④：幂等——再跑一次 → 行数仍 3、哨兵仍在、各子集仍非空 ──
            var result2 = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[复跑] NetMetricUpserts={result2.NetworkMetricUpserts}");

            var totalAfter = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F网点编码]=@code",
                ("@org", OrgId), ("@code", NetCode));
            Assert.Equal(3, totalAfter);

            var noSteal16After = await CountAsync(conn,
                $@"SELECT COUNT(*) FROM [{MetricTable}]
                   WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code
                     AND [F一阶段及时签收率] IS NOT NULL AND [F签收率考核金额]=77.77",
                ("@org", OrgId), ("@d", Date16), ("@code", NetCode));
            _log.WriteLine($"[幂等] 复跑后 6-16 行 签收率 + 哨兵 77.77 保持 行数={noSteal16After}（期望1）");
            Assert.Equal(1, noSteal16After);

            var penetrationAfter = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code AND [F自建渗透率] IS NOT NULL",
                ("@org", OrgId), ("@d", Date01), ("@code", NetCode));
            Assert.Equal(1, penetrationAfter);
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

    private async Task AssertNotNull(string conn, DateTime date, string col)
    {
        var n = await CountAsync(conn,
            $"SELECT COUNT(*) FROM [{MetricTable}] WHERE [FOrgId]=@org AND [F业务日期]=@d AND [F网点编码]=@code AND [{col}] IS NOT NULL",
            ("@org", OrgId), ("@d", date), ("@code", NetCode));
        _log.WriteLine($"[子集非空 {date:MM-dd}] [{col}] 非空行数={n}（期望1）");
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
            FBatchNo = $"TESTC3B5-{Guid.NewGuid():N}",
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

    /// <summary>删 4 源 STG 残留行（按网点编码/名称，避免跨批次去重把本次导入吞掉——真库测试已知坑）。</summary>
    private static async Task CleanupStgResidueAsync(string conn)
    {
        // 有编码列的源按编码删；无编码列（末端派送/拦截）按网点名称删。
        await ExecAsync(conn, $"DELETE FROM [{SignRateTable}] WHERE [FOrgId]=@org AND [F网点编号]=@code", ("@org", OrgId), ("@code", NetCode));
        await ExecAsync(conn, $"DELETE FROM [{PenetrationTable}] WHERE [FOrgId]=@org AND [F网点编号]=@code", ("@org", OrgId), ("@code", NetCode));
        await ExecAsync(conn, $"DELETE FROM [{DeliveryTable}] WHERE [FOrgId]=@org AND [F应签所属网点]=@name", ("@org", OrgId), ("@name", NetFullName));
        await ExecAsync(conn, $"DELETE FROM [{InterceptTable}] WHERE [FOrgId]=@org AND [F所属网点]=@name", ("@org", OrgId), ("@name", NetFullName));
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
