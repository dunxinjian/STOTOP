using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
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
/// Plan2 Phase C — C4 Step1/2：归一重跑回填 <see cref="IQualityUnificationService.RematchUnresolvedAsync"/> 集成测试（连开发测试库 stotop）。
///
/// 语义验证：RematchUnresolved <b>只重解析「未匹配」历史质量事件的主数据，不重建事件、不重导</b>。
///   场景（揽收分析明细事件源，网点仅名称、员工仅脏名无工号）：
///     Arrange：建 STG/QL 表；预清残留；<b>直接 INSERT 几行揽收分析 STG</b>（脏网点名 + 脏员工名，无工号，F揽收及时标识=否过滤命中）；
///              种子网点 320288(全称=江苏太仓市城区公司) + 业务员(工号 SALTEST_RM01/网点320288)；
///              <b>先不种别名</b> → UnifyShentongAsync(192)：脏网点名既≠全称又无别名→网点状态0；员工无工号且网点未匹配→无法启发式→状态0。
///     断言：确有未匹配事件（网点状态0 + 员工状态0），记录事件总数 N0。
///     Act：补种 ExpNetworkPointAlias(脏网点名→320288) + ExpSalesmanAlias(脏员工名→SALTEST_RM01) → RematchUnresolvedAsync(192)。
///     断言：网点 0→1 回填 F网点编码=320288；员工 0→2 回填 F员工工号/F员工ID；事件总数仍 N0（<b>不产生新事件行</b>）；
///           幂等（再 rematch 无变化、返回 0 回填）。
///   清理：try/finally 删本 org QL 事件/字典 + STG 行 + 种子网点/业务员/别名。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class RematchUnresolvedTests
{
    private const long OrgId = 192;
    private const string NetCode = "320288";
    private const string NetFullName = "江苏太仓市城区公司";
    private const string EventTable = "QL申通_承运商质量事件";
    private const string DictTable = "QL申通_质量问题字典";
    private const string StgTable = ShentongSourceMap.PickupAnalysisTable; // STG申通_揽收分析明细

    // 故意的脏值：既不等网点全称、也无主数据精确命中，只有补别名才能命中（演示 0→匹配）。
    private const string DirtyNetName = "太仓城区RM测试脏名";
    private const string DirtyEmpName = "重跑测试员RM脏名";
    private const string EmpNo = "SALTEST_RM01";
    private const long EmpId = 99000201L;

    private readonly ITestOutputHelper _log;
    public RematchUnresolvedTests(ITestOutputHelper log) => _log = log;

    [SkippableFact]
    public async Task RematchUnresolved_RebindsNetworkAndEmployee_NoNewEvents_Idempotent()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接，跳过集成测试");
        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        await using var db = CreateDbContext(conn);
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }

        CardFlowSeeder.Migrate(db);
        QualityUnifySeeder.EnsureTables(db);

        long batchId = 0;
        try
        {
            // ── Arrange：预清 + 种子 ──
            await CleanupAllAsync(conn);
            await EnsureNetworkAsync(db);
            await EnsureSalesmanAsync(db);

            // 直接 INSERT 3 行揽收分析 STG（脏网点名 + 脏员工名 + 无工号 + 过滤命中「否」）。
            batchId = await InsertStgRowsAsync(db, 3);
            var stgRows = await CountAsync(conn, $"SELECT COUNT(*) FROM [{StgTable}] WHERE [F批次ID]=@b", ("@b", batchId));
            _log.WriteLine($"[Arrange] STG 行数={stgRows}（期望3）");
            Assert.Equal(3, stgRows);

            var svc = new QualityUnificationService(db, new MasterDataMatcher(db));

            // ── 首跑归一：脏名既不命中网点全称又无别名 → 网点状态0；员工无工号+网点未匹配 → 状态0 ──
            var first = await svc.UnifyShentongAsync(OrgId);
            _log.WriteLine($"[首跑] Events={first.EventsUpserted} NetUnmatched={first.NetworkUnmatched} EmpUnmatched={first.EmployeeUnmatched}");

            var n0 = await EventCountAsync(conn, batchId);
            _log.WriteLine($"[首跑] 本批事件总数 N0={n0}（期望3）");
            Assert.Equal(3, n0);

            // 确有未匹配：网点状态0 全部、员工状态0 全部
            var netUnmatched = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@tbl AND [F来源批次ID]=@b AND [F网点匹配状态]=0",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId));
            var empUnmatched = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@tbl AND [F来源批次ID]=@b AND [F员工匹配状态]=0",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId));
            _log.WriteLine($"[首跑] 网点未匹配={netUnmatched} 员工未匹配={empUnmatched}（各期望3）");
            Assert.Equal(3, netUnmatched);
            Assert.Equal(3, empUnmatched);

            // ── Act：补种别名（脏网点名→320288 / 脏员工名→工号）后重跑回填 ──
            await EnsureAliasesAsync(db);
            var rm = await svc.RematchUnresolvedAsync(OrgId);
            _log.WriteLine($"[重跑] NetworkRebound={rm.NetworkRebound} EmployeeRebound={rm.EmployeeRebound} Scanned={rm.Scanned}");
            Assert.Equal(3, rm.NetworkRebound);
            Assert.Equal(3, rm.EmployeeRebound);

            // ── 断言：网点 0→1、F网点编码=320288 回填 ──
            var netBound = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@tbl AND [F来源批次ID]=@b AND [F网点匹配状态]=1 AND [F网点编码]=@code",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId), ("@code", NetCode));
            _log.WriteLine($"[重跑] 网点状态1+编码{NetCode} 行数={netBound}（期望3）");
            Assert.Equal(3, netBound);

            // ── 断言：员工 0→2、F员工工号/F员工ID 回填 ──
            var empBound = await CountAsync(conn,
                $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@tbl AND [F来源批次ID]=@b AND [F员工匹配状态]=2 AND [F员工工号]=@no AND [F员工ID]=@id",
                ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId), ("@no", EmpNo), ("@id", EmpId));
            _log.WriteLine($"[重跑] 员工状态2+工号{EmpNo}+ID{EmpId} 行数={empBound}（期望3）");
            Assert.Equal(3, empBound);

            // ── 断言：不产生新事件行（总数仍 N0）──
            var n1 = await EventCountAsync(conn, batchId);
            _log.WriteLine($"[重跑] 事件总数 N1={n1}（期望仍 {n0}）");
            Assert.Equal(n0, n1);

            // ── 断言：幂等——再 rematch 无回填、事件数不变 ──
            var rm2 = await svc.RematchUnresolvedAsync(OrgId);
            _log.WriteLine($"[幂等] NetworkRebound={rm2.NetworkRebound} EmployeeRebound={rm2.EmployeeRebound} Scanned={rm2.Scanned}");
            Assert.Equal(0, rm2.NetworkRebound);
            Assert.Equal(0, rm2.EmployeeRebound);
            var n2 = await EventCountAsync(conn, batchId);
            Assert.Equal(n0, n2);
        }
        finally
        {
            await CleanupAllAsync(conn);
            await CleanupBatchAsync(conn, batchId);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Arrange helper
    // ─────────────────────────────────────────────────────────────

    private static async Task EnsureNetworkAsync(STOTOPDbContext db)
    {
        var exists = await db.Set<ExpNetworkPoint>().IgnoreQueryFilters()
            .AnyAsync(np => np.FCode == NetCode && np.FOrgId == OrgId);
        if (exists) return;
        db.Set<ExpNetworkPoint>().Add(new ExpNetworkPoint
        {
            FCode = NetCode,
            FFullName = NetFullName,
            FOrgId = OrgId,
            FOwnerOrgId = OrgId,
        });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureSalesmanAsync(STOTOPDbContext db)
    {
        var exists = await db.Set<ExpSalesman>().IgnoreQueryFilters()
            .AnyAsync(s => s.FEmployeeNo == EmpNo);
        if (exists) return;
        db.Set<ExpSalesman>().Add(new ExpSalesman
        {
            FEmployeeNo = EmpNo,
            FNetworkPointCode = NetCode,
            FEmployeeId = EmpId,
            FName = "重跑测试真名",
            FStatus = 1,
        });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureAliasesAsync(STOTOPDbContext db)
    {
        db.Set<ExpNetworkPointAlias>().Add(new ExpNetworkPointAlias
        {
            FName = DirtyNetName,
            FNetworkPointCode = NetCode,
            FOrgId = OrgId,
        });
        db.Set<ExpSalesmanAlias>().Add(new ExpSalesmanAlias
        {
            FName = DirtyEmpName,
            FEmployeeNo = EmpNo,
            FOrgId = OrgId,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>直接 INSERT n 行揽收分析 STG（脏网点名/脏员工名/无工号/过滤命中），返回批次ID。</summary>
    private static async Task<long> InsertStgRowsAsync(STOTOPDbContext db, int n)
    {
        var batch = new CfBatch
        {
            FFlowDefinitionId = 2303,
            FOrgId = OrgId,
            FTriggeredById = 1,
            FTriggeredTime = DateTime.Now,
            FTriggerType = "fileUpload",
            FBatchNo = $"TESTC4RM-{Guid.NewGuid():N}",
            FStatus = CfBatchStatus.Parsing,
            FUploadMethod = "auto",
            FCreatedTime = DateTime.Now,
        };
        db.Set<CfBatch>().Add(batch);
        await db.SaveChangesAsync();
        var batchId = batch.FID;

        for (int i = 0; i < n; i++)
        {
            db.Set<StgShentongPickupAnalysis>().Add(new StgShentongPickupAnalysis
            {
                F批次ID = batchId,
                FOrgId = OrgId,
                F处理状态 = 0,
                FIsRevoked = false,
                F统计日期 = "2026-06-10",
                F运单编号 = $"RMWB{i:0000}",
                F揽收所属网点 = DirtyNetName,
                F收件员 = DirtyEmpName,
                F揽收及时标识 = "否", // 过滤命中（仅未及时入事件）
                F电商平台 = "测试平台",
                F创建时间 = DateTime.Now,
            });
        }
        await db.SaveChangesAsync();
        return batchId;
    }

    // ─────────────────────────────────────────────────────────────
    // 查询 / 清理 helper
    // ─────────────────────────────────────────────────────────────

    private static Task<int> EventCountAsync(string conn, long batchId) =>
        CountAsync(conn,
            $"SELECT COUNT(*) FROM [{EventTable}] WHERE [FOrgId]=@org AND [F来源STG表]=@tbl AND [F来源批次ID]=@b",
            ("@org", OrgId), ("@tbl", StgTable), ("@b", batchId));

    private static async Task CleanupAllAsync(string conn)
    {
        await ExecAsync(conn, $"DELETE FROM [{EventTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
        await ExecAsync(conn, $"DELETE FROM [{DictTable}] WHERE [FOrgId]=@org", ("@org", OrgId));
        // STG 残留：按脏网点名删本测试行（不会误删真实样例）。
        await ExecAsync(conn, $"DELETE FROM [{StgTable}] WHERE [FOrgId]=@org AND [F揽收所属网点]=@name",
            ("@org", OrgId), ("@name", DirtyNetName));
        // 种子网点/业务员/别名
        await ExecAsync(conn, "DELETE FROM [EXP快递网点] WHERE [F编号]=@code AND [F组织ID]=@org", ("@code", NetCode), ("@org", OrgId));
        await ExecAsync(conn, "DELETE FROM [EXP业务员] WHERE [F工号]=@no", ("@no", EmpNo));
        await ExecAsync(conn, "DELETE FROM [EXP快递网点名称映射] WHERE [F名称]=@name AND [F组织ID]=@org", ("@name", DirtyNetName), ("@org", OrgId));
        await ExecAsync(conn, "DELETE FROM [EXP快递业务员名称映射] WHERE [F名称]=@name AND [F组织ID]=@org", ("@name", DirtyEmpName), ("@org", OrgId));
    }

    private async Task CleanupBatchAsync(string conn, long batchId)
    {
        if (batchId <= 0) return;
        try { await ExecAsync(conn, $"DELETE FROM [{StgTable}] WHERE [F批次ID]=@b", ("@b", batchId)); }
        catch { /* ignore */ }
        try
        {
            await using var cleanup = CreateDbContext(conn);
            var b = await cleanup.Set<CfBatch>().FirstOrDefaultAsync(x => x.FID == batchId);
            if (b != null) { cleanup.Set<CfBatch>().Remove(b); await cleanup.SaveChangesAsync(); }
        }
        catch { /* ignore */ }
    }

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
