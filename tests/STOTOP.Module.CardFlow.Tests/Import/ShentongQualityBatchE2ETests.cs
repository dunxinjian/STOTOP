using System.Threading.Channels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.AutoPlugin.Implementations;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Import;
using STOTOP.Module.CardFlow.Services.Import.TransformEngine;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.OA.Entities;
using STOTOP.Module.System.Entities;
using STOTOP.WebAPI.Data.Seeders;
using Xunit;
using Xunit.Abstractions;

namespace STOTOP.Module.CardFlow.Tests.Import;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// Plan 1 Phase C — C1：申通网点质控「整批导入」综合 e2e 集成测试（连开发测试库 stotop）。
/// 这是 Plan 1 的综合验收门禁，覆盖三部分：
///
/// Part 1 — 路由覆盖（最高价值）：遍历两个样例目录全部数据文件（过滤 ~$ 临时文件），
///   用 ExcelParserService.ReadHeadersAsync 读 sheet0 首行 → BatchTriggerService.ClassifyFilesAsync(org=192)
///   分类。核心断言：没有任何数据文件 ambiguous（多规则同时命中＝某两源 columnIdentifier 撞了），
///   也没有 unmatched（每个文件都精确路由到唯一 flow）。并断言覆盖 9 大域全部 routed。
///
/// Part 2 — 真导入行数核对（代表性抽样）：挑 7 个覆盖各路由/表头类型的源，复用 B0 的
///   new ExcelInputPlugin(...).ExecuteAsync 同步路径真导入，按 F批次ID 断言行数（python 实读为期望值）。
///   每个导入后清理本批 STG 行 + CfBatch，避免污染 stotop。
///
/// Part 3 — 幂等：对未到件源连续导入两次，断言第二次 0 行新写入、合计不增（跨批去重生效、不丢整批）。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop，避免并行跨批去重竞态
public class ShentongQualityBatchE2ETests
{
    private const long OrgId = 192;

    private static readonly string 明细Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据明细";
    private static readonly string 展示页Dir =
        @"E:\STOTOP_Fable\Taicang\网点质控数据\申通网点\申通数据展示页";

    private readonly ITestOutputHelper _log;
    public ShentongQualityBatchE2ETests(ITestOutputHelper log) => _log = log;

    // ─────────────────────────────────────────────────────────────
    // 基础设施
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 把样例文件快照到临时目录后返回临时路径。样例目录里的文件可能正被 Excel 打开（见 ~$ 临时文件），
    /// 而插件用 FileShare.Read 打开会被独占锁拒绝。这里以 FileShare.ReadWrite|Delete 读源、复制到 %TEMP%，
    /// 让路由/导入都从快照读，与「源文件是否在 Excel 打开」彻底解耦（测试环境健壮性，非生产逻辑变更）。
    /// </summary>
    private static string Snapshot(string srcPath)
    {
        var dir = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "stqc_e2e_snap");
        Directory.CreateDirectory(dir);
        // 保留原文件名（含扩展名），路由的 fileNamePattern 与魔数判别都依赖原名/原内容
        var dst = global::System.IO.Path.Combine(dir, global::System.IO.Path.GetFileName(srcPath));
        using (var src = new FileStream(srcPath, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        using (var outFs = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            src.CopyTo(outFs);
        }
        return dst;
    }

    private static void RegisterModules()
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(CfCard).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(OaExpenseRequest).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(SysUser).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(FinVoucher).Assembly);
    }

    private static STOTOPDbContext CreateDbContext(string conn)
    {
        RegisterModules();
        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseSqlServer(conn)
            .Options;
        return new STOTOPDbContext(options);
    }

    /// <summary>
    /// 构建真 DI 容器（STOTOPDbContext 走真 SQL 连接），供 BatchTriggerService 内部 CreateScope() 取同一库。
    /// </summary>
    private static ServiceProvider BuildSqlProvider(string conn)
    {
        RegisterModules();
        var services = new ServiceCollection();
        services.AddDbContext<STOTOPDbContext>(opt => opt.UseSqlServer(conn));
        return services.BuildServiceProvider();
    }

    private static BatchTriggerService CreateTriggerService(ServiceProvider provider)
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var channel = Channel.CreateUnbounded<BatchJob>();
        return new BatchTriggerService(scopeFactory, channel, NullLogger<BatchTriggerService>.Instance);
    }

    private sealed class NoopProgressReporter : IPluginProgressReporter
    {
        public Task ReportProgressAsync(long batchId, int processedRows, int totalRows, string? currentStep = null) => Task.CompletedTask;
        public Task ReportErrorAsync(long batchId, int rowIndex, string errorMessage) => Task.CompletedTask;
        public Task ReportCompletedAsync(long batchId, bool success, string? message = null) => Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────
    // Part 1：路由覆盖
    // ─────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Part1_RouteCoverage_AllRouted_NoAmbiguous_NoUnmatched()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        Skip.IfNot(Directory.Exists(明细Dir) && Directory.Exists(展示页Dir), "样例数据目录不存在");

        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        // 可达性兜底
        await using (var probe = CreateDbContext(conn))
        {
            try { await probe.Database.OpenConnectionAsync(); await probe.Database.CloseConnectionAsync(); }
            catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }
            CardFlowSeeder.Migrate(probe);
        }

        var provider = BuildSqlProvider(conn);
        var trigger = CreateTriggerService(provider);
        var parser = new ExcelParserService();

        // 遍历两个目录全部数据文件（过滤 ~$ 临时文件 + 仅 excel/csv）
        var files = new List<FileColumnHeader>();
        foreach (var dir in new[] { 明细Dir, 展示页Dir })
        {
            foreach (var path in Directory.GetFiles(dir).OrderBy(p => p))
            {
                var name = global::System.IO.Path.GetFileName(path);
                if (name.StartsWith("~$")) continue;
                var ext = global::System.IO.Path.GetExtension(name).ToLowerInvariant();
                if (ext is not (".xls" or ".xlsx" or ".csv")) continue;

                // 源文件可能正被 Excel 打开，快照到临时目录后再读（保留原名供 fileNamePattern 路由）
                var snap = Snapshot(path);
                await using var fs = File.OpenRead(snap);
                // upload-auto 路由读 sheet0（首个 sheet）首行做内容路由
                var cols = await parser.ReadHeadersAsync(fs, name, headerRow: 1);
                files.Add(new FileColumnHeader(name, cols.ToList()));
            }
        }

        Assert.True(files.Count >= 31, $"应至少遍历到 31 个数据文件，实际 {files.Count}");

        var result = await trigger.ClassifyFilesAsync(files, OrgId);

        // 输出文件→flow 映射表，便于核对 9 大域覆盖
        _log.WriteLine($"=== Part1 路由分类：输入 {files.Count}，routed {result.Routed.Count}，"
                       + $"unmatched {result.Unmatched.Count}，ambiguous {result.Ambiguous.Count} ===");
        foreach (var r in result.Routed.OrderBy(x => x.FlowDefinitionId))
            _log.WriteLine($"  ROUTED   flow={r.FlowDefinitionId} rule={r.PluginRuleId}  <- {r.FileName}");
        foreach (var u in result.Unmatched)
            _log.WriteLine($"  UNMATCHED <- {u.FileName}  cols=[{string.Join(",", u.Columns)}]");
        foreach (var a in result.Ambiguous)
            _log.WriteLine($"  AMBIGUOUS <- {a.FileName}  candidates=["
                           + string.Join(" | ", a.Candidates.Select(c => $"flow{c.FlowDefinitionId}/rule{c.PluginRuleId}")) + "]");

        // 核心断言 1：没有任何文件多规则同时命中（columnIdentifier 撞库 / 误路由的关键风险）
        Assert.Empty(result.Ambiguous);
        // 核心断言 2：每个数据文件都精确路由到唯一 flow（无认领失败）
        Assert.Empty(result.Unmatched);
        // 全部 routed
        Assert.Equal(files.Count, result.Routed.Count);

        // 覆盖 9 大域：用命中的 flow 集合校验各域均有 routed 文件。
        // 域代表 flow（按设计）：完整性2301 / 及时准确2302 / 揽收2303 / 出仓2304+2323 / 滞留2305+2327 /
        //   派送签收2306+2307+2325 / 积压遗失2308+2309+2326 / 投诉虚签2310+2311+2312+2313 /
        //   质检履约送货2314+2315+2316 / 拦截2317+2328 / 渗透2318 / 物流信息2319 / 签收率2322 / 小件员2324 / 揽收考核2329
        var routedFlows = result.Routed.Select(r => r.FlowDefinitionId).ToHashSet();
        // 物流信息指数多 sheet：upload-auto 读 sheet0（及时性）→ 命中及时 flow 2319（不是 ambiguous）
        Assert.Contains(2319L, routedFlows);
        // 双行表头/退化表头/misnamed 各代表
        Assert.Contains(2301L, routedFlows); // 完整性明细（未到/未揽/未派 共用列集）
        Assert.Contains(2311L, routedFlows); // 投诉账单（双行表头，靠 fileNamePattern）
        Assert.Contains(2324L, routedFlows); // 小件员履约（双行表头/员工级，靠 fileNamePattern）
        Assert.Contains(2322L, routedFlows); // 签收率考核（退化表头，靠 columnIdentifier）
        Assert.Contains(2314L, routedFlows); // 照片质检（misnamed xls）

        // 9 大域均有命中（每域取一个代表 flow）
        foreach (var domainFlow in new[] { 2301L, 2303L, 2304L, 2305L, 2306L, 2308L, 2310L, 2314L, 2317L })
            Assert.Contains(domainFlow, routedFlows);

        await provider.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Part 2：真导入行数核对（代表性抽样）
    // ─────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Part2_RepresentativeImports_RowCountsMatchFiles()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        await using var db = CreateDbContext(conn);
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }
        CardFlowSeeder.Migrate(db);

        // (源描述, 文件路径, flow, stage, rule, 表名, 期望行数, 非空校验列们)
        // 期望行数由 python 实读对应文件数据行得到（按各规则 headerRow/dataStartRow）。
        var cases = new (string Desc, string Path, long Flow, long Stage, long Rule, string Table, int Expected, string[] NonEmptyCols)[]
        {
            // 内容路由明细：未到件 → 物流完整性明细，15 行，问题类型=未到件
            ("未到件(内容路由明细)", global::System.IO.Path.Combine(明细Dir, "excel (未到件).xls"),
                2301, 5101, 3101, "STG申通_物流完整性明细", 15, new[] { "F运单号", "F问题类型" }),
            // 揽收分析（内容路由明细，单行表头 24 列）：文件 455 数据行，但去重键 F运单编号 仅 422 个不同值
            // （26 个运单编号出现多次＝33 重复行），规则 3103 按 F运单编号 去重 → 入库 422 行。
            ("揽收分析(内容路由明细,运单编号去重)", global::System.IO.Path.Combine(明细Dir, "订单揽收分析明细V3-1c76e08a7b4e4ccab6e43eef55a2ee94.xlsx"),
                2303, 5103, 3103, "STG申通_揽收分析明细", 422, new[] { "F运单编号" }),
            // 双行表头：投诉账单（headerRow=2/dataStartRow=3）：文件 29 数据行，去重键 F运单号+F账单生成时间
            // 有 1 组重复 → 入库 28 行。
            ("投诉账单(双行表头,运单+账单时间去重)", global::System.IO.Path.Combine(明细Dir, "收到的投诉账单_账单明细_20260617_1781686058721.xlsx"),
                2311, 5111, 3111, "STG申通_投诉账单明细", 28, new[] { "F运单号" }),
            // 退化表头：签收率未达标考核（headerRow=1/dataStartRow=3，跳过子字段名行）→ 1 行（仅 1 网点）
            ("签收率考核(退化表头 ds=3)", global::System.IO.Path.Combine(展示页Dir, "ossdfa26db73e7f4f548c21e65749ea6d65（签收率未达标考核）.xlsx"),
                2322, 5122, 3122, "STG申通_签收率考核汇总", 1, new[] { "F网点编号", "F应签量" }),
            // misnamed xls（实为 xlsx）：抖音照片质检 → 1851 行
            ("照片质检(misnamed xls)", global::System.IO.Path.Combine(明细Dir, "b20606c963a5471eaf1e443c1aa42564抖音照片质检.xls"),
                2314, 5114, 3114, "STG申通_照片质检明细", 1851, new[] { "F单号" }),
        };

        var batchIds = new List<(long id, string table)>();
        // 小件员（员工级双行表头）单独处理：merged 表头会让 F所属网点/F所属小件员 落到 Column 占位列，
        // 故行数 + 一个 row2 可靠映射列(F当日派签量)非空作硬断言，并额外统计 F所属小件员 非空数（如实暴露）。
        long courierBatch = 0;
        // 物流信息指数多 sheet：三规则各触发一次（B18 已专测，这里并入复测 sheetName 路由真生效）。
        long timelyBatch = 0, completeBatch = 0, accurateBatch = 0;

        try
        {
            foreach (var c in cases)
            {
                Skip.IfNot(File.Exists(c.Path), $"样例文件不存在: {c.Path}");
                var (id, result) = await ImportOnceAsync(db, conn, c.Path, c.Flow, c.Stage, c.Rule);
                batchIds.Add((id, c.Table));

                Assert.True(result.Success, $"[{c.Desc}] 导入应成功，实际 Message={result.Message}");

                var rows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{c.Table}] WHERE [F批次ID] = @b", id);
                _log.WriteLine($"[{c.Desc}] 期望 {c.Expected} 行，实际 {rows} 行 (table={c.Table}, batch={id})");
                Assert.Equal(c.Expected, rows);

                foreach (var col in c.NonEmptyCols)
                {
                    var empty = await CountAsync(conn,
                        $"SELECT COUNT(*) FROM [{c.Table}] WHERE [F批次ID] = @b AND ([{col}] IS NULL OR LTRIM(RTRIM(CAST([{col}] AS NVARCHAR(MAX)))) = N'')", id);
                    Assert.True(empty == 0, $"[{c.Desc}] 列 [{col}] 应全部非空，实际有 {empty} 行为空");
                }
            }

            // 未到件额外校验：问题类型全为「未到件」
            var 未到件Batch = batchIds.First(x => x.table == "STG申通_物流完整性明细").id;
            var wrongType = await CountAsync(conn,
                "SELECT COUNT(*) FROM [STG申通_物流完整性明细] WHERE [F批次ID] = @b AND ([F问题类型] IS NULL OR [F问题类型] <> N'未到件')",
                未到件Batch);
            Assert.Equal(0, wrongType);

            // ── 小件员履约（双行表头/员工级，靠 fileNamePattern 路由）──
            var courierPath = global::System.IO.Path.Combine(展示页Dir, "小件员履约指标历史数据导出_导出任务_20260617_109361182.xlsx");
            Skip.IfNot(File.Exists(courierPath), $"样例文件不存在: {courierPath}");
            (courierBatch, var courierResult) = await ImportOnceAsync(db, conn, courierPath, 2324, 5124, 3124);
            Assert.True(courierResult.Success, $"[小件员履约] 导入应成功，实际 Message={courierResult.Message}");

            var courierRows = await CountAsync(conn, "SELECT COUNT(*) FROM [STG申通_小件员履约指标] WHERE [F批次ID] = @b", courierBatch);
            // python 实读：row3..row61 共 59 个小件员
            _log.WriteLine($"[小件员履约] 期望 59 行，实际 {courierRows} 行 (batch={courierBatch})");
            Assert.Equal(59, courierRows);

            // row2 可靠映射列（当日派签量）应全部非空，证明 headerRow=2 表头映射生效
            var courierEmptyQty = await CountAsync(conn,
                "SELECT COUNT(*) FROM [STG申通_小件员履约指标] WHERE [F批次ID] = @b AND ([F当日派签量] IS NULL OR LTRIM(RTRIM(CAST([F当日派签量] AS NVARCHAR(MAX)))) = N'')",
                courierBatch);
            Assert.Equal(0, courierEmptyQty);

            // F所属小件员（merged 表头列）非空数：如实记录并断言全非空（若 merged 列丢失会暴露为 < 59）
            var courierNamed = await CountAsync(conn,
                "SELECT COUNT(*) FROM [STG申通_小件员履约指标] WHERE [F批次ID] = @b AND [F所属小件员] IS NOT NULL AND LTRIM(RTRIM([F所属小件员])) <> N''",
                courierBatch);
            _log.WriteLine($"[小件员履约] F所属小件员 非空行数 = {courierNamed} / {courierRows}");
            Assert.Equal(courierRows, courierNamed);

            // ── 物流信息指数：同文件三规则各触发，sheetName 路由各 sheet 到对应表 ──
            var infoPath = global::System.IO.Path.Combine(展示页Dir, "excel（物流信息指数）.xls");
            Skip.IfNot(File.Exists(infoPath), $"样例文件不存在: {infoPath}");
            (timelyBatch, var rt) = await ImportOnceAsync(db, conn, infoPath, 2319, 5119, 3119);
            (completeBatch, var rc) = await ImportOnceAsync(db, conn, infoPath, 2320, 5120, 3120);
            (accurateBatch, var ra) = await ImportOnceAsync(db, conn, infoPath, 2321, 5121, 3121);
            Assert.True(rt.Success && rc.Success && ra.Success, "物流信息指数三 sheet 导入应均成功");

            var timely = await CountAsync(conn, "SELECT COUNT(*) FROM [STG申通_物流信息及时汇总] WHERE [F批次ID] = @b", timelyBatch);
            var complete = await CountAsync(conn, "SELECT COUNT(*) FROM [STG申通_物流信息完整汇总] WHERE [F批次ID] = @b", completeBatch);
            var accurate = await CountAsync(conn, "SELECT COUNT(*) FROM [STG申通_物流信息准确汇总] WHERE [F批次ID] = @b", accurateBatch);
            _log.WriteLine($"[物流信息指数] 及时={timely}(期望1) 完整={complete}(期望2) 准确={accurate}(期望1)");
            Assert.Equal(1, timely);
            Assert.Equal(2, complete);
            Assert.Equal(1, accurate);
            // 对的 sheet 落对的表
            var completeHasCol = await CountAsync(conn,
                "SELECT COUNT(*) FROM [STG申通_物流信息完整汇总] WHERE [F批次ID] = @b AND [F揽收缺失量] IS NOT NULL", completeBatch);
            Assert.Equal(2, completeHasCol);
            var accurateHasCol = await CountAsync(conn,
                "SELECT COUNT(*) FROM [STG申通_物流信息准确汇总] WHERE [F批次ID] = @b AND [F到件不准确率] IS NOT NULL", accurateBatch);
            Assert.Equal(1, accurateHasCol);
        }
        finally
        {
            foreach (var (id, table) in batchIds) await CleanupBatchAsync(conn, table, id);
            await CleanupBatchAsync(conn, "STG申通_小件员履约指标", courierBatch);
            await CleanupBatchAsync(conn, "STG申通_物流信息及时汇总", timelyBatch);
            await CleanupBatchAsync(conn, "STG申通_物流信息完整汇总", completeBatch);
            await CleanupBatchAsync(conn, "STG申通_物流信息准确汇总", accurateBatch);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Part 3：幂等（跨批去重生效、不丢整批）
    // ─────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Part3_Idempotency_SecondImportAddsNoRows()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        await using var db = CreateDbContext(conn);
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }
        CardFlowSeeder.Migrate(db);

        const string table = "STG申通_物流完整性明细";
        var path = global::System.IO.Path.Combine(明细Dir, "excel (未到件).xls");
        Skip.IfNot(File.Exists(path), $"样例文件不存在: {path}");

        long b1 = 0, b2 = 0;
        try
        {
            (b1, var r1) = await ImportOnceAsync(db, conn, path, 2301, 5101, 3101);
            Assert.True(r1.Success, $"首次导入应成功，实际 Message={r1.Message}");
            var first = await CountAsync(conn, $"SELECT COUNT(*) FROM [{table}] WHERE [F批次ID] = @b", b1);
            Assert.Equal(15, first);

            // 第二批：全部命中「运单号+问题类型」跨批去重 → 0 行新写入（soft-fail，不是回退）
            (b2, var r2) = await ImportOnceAsync(db, conn, path, 2301, 5101, 3101);
            Assert.False(r2.IsCritical, "重复导入全去重不应是严重失败");
            Assert.Contains("去重", r2.Message ?? "");

            var second = await CountAsync(conn, $"SELECT COUNT(*) FROM [{table}] WHERE [F批次ID] = @b", b2);
            Assert.Equal(0, second);

            // 两批合计仍 15：既不重复计数，也不因唯一约束放弃整批
            var total = await CountAsync2(conn, $"SELECT COUNT(*) FROM [{table}] WHERE [F批次ID] IN (@b, @b2)", b1, b2);
            _log.WriteLine($"[幂等] 第一批 {first}、第二批 {second}、合计 {total}（期望 15/0/15）");
            Assert.Equal(15, total);
        }
        finally
        {
            await CleanupBatchAsync(conn, table, b1);
            await CleanupBatchAsync(conn, table, b2);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 导入与清理 helper
    // ─────────────────────────────────────────────────────────────

    private async Task<(long batchId, PluginResult result)> ImportOnceAsync(
        STOTOPDbContext db, string conn, string filePath, long flowDefinitionId, long stageId, long ruleId)
    {
        // 快照到临时目录，避免源文件被 Excel 打开时插件 FileShare.Read 打开失败
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
            FBatchNo = $"TESTC1-{Guid.NewGuid():N}",
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

    private static async Task<int> CountAsync2(string conn, string sql, long b1, long b2)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@b", b1);
        cmd.Parameters.AddWithValue("@b2", b2);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
