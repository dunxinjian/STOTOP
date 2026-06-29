using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.OA.Entities;
using STOTOP.Module.System.Entities;
using STOTOP.WebAPI.Data.Seeders;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Import;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// 回归：STG 暂存表 F账套ID 与 DDL（[F账套ID] BIGINT NULL）对齐为 long? 后，
/// 读取「F账套ID 为 NULL」的整行实体不再抛 System.Data.SqlTypes.SqlNullValueException。
///
/// 背景：无账套批次（CfBatch.FAccountSetId 为 null）导入时，ExcelInputPlugin 把 F账套ID
/// 写成 DBNull → DB 落 NULL；修复前实体把该列声明为非空 long，EF 物化整行即抛
/// SqlNullValueException（申通网点质控 Plan 2 归一时踩到，曾临时用 .Select 投影绕开）。
///
/// 本测试用裸 SQL 插一条 F账套ID 为 NULL 的行（FOrgId 给真值以隔离 F账套ID 单一变量），
/// 再用 IgnoreQueryFilters 读整行实体——确保该行被物化（绕开组织全局过滤器，把变量收敛到
/// 「物化 NULL→long?」这一处）。修复前此读取抛异常（红），修复后返回单行且 F账套ID==null（绿）。
/// 连开发测试库 stotop；无连接/不可达时 Skip。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class StgNullAccountSetMaterializationTests
{
    private const string TableName = "STG申通_物流完整性明细";
    private const long OrgId = 192;
    // 负数 sentinel 批次号：真实导入不会使用，避免与真数据撞车
    private const long SentinelBatchId = -987654321L;

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

    [SkippableFact]
    public async Task ReadingStgEntity_WithNullAccountSetId_DoesNotThrow()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接（未设 STOTOP_TEST_CONNECTION 且主树 db-connections.json 不可读），跳过集成测试");

        var conn = TestSqlConnection.GetConnectionString()!;
        TestSqlConnection.EnsureSystemConnectionFile();

        await using var db = CreateDbContext(conn);

        // 可达性兜底：连不上则跳过而非失败
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }

        // 幂等建表（STG 前缀表 EF 不自动建）
        CardFlowSeeder.Migrate(db);

        try
        {
            // 先清掉可能残留的本 sentinel 批次行，保证可重复运行
            await ExecAsync(conn, $"DELETE FROM [{TableName}] WHERE [F批次ID] = @b", ("@b", SentinelBatchId));

            // 裸 SQL 插一条 F账套ID 为 NULL 的行；FOrgId 给真值，隔离到 F账套ID 单一变量
            await ExecAsync(conn,
                $"INSERT INTO [{TableName}] ([F批次ID],[FOrgId],[F账套ID],[F处理状态],[F运单号],[F问题类型]) " +
                "VALUES (@b, @org, NULL, 0, N'TEST-NULL-ACCT', N'未到件')",
                ("@b", SentinelBatchId), ("@org", OrgId));

            // 关键断言点：读「整行实体」。修复前这里抛 SqlNullValueException；
            // IgnoreQueryFilters 绕开组织全局过滤器，确保该行确实被选出并物化。
            var rows = await db.Set<StgShentongLogisticsCompleteness>()
                .IgnoreQueryFilters()
                .Where(x => x.F批次ID == SentinelBatchId)
                .ToListAsync();

            Assert.Single(rows);
            Assert.Null(rows[0].F账套ID); // NULL 列 → long? 的 null，不再抛异常
        }
        finally
        {
            await ExecAsync(conn, $"DELETE FROM [{TableName}] WHERE [F批次ID] = @b", ("@b", SentinelBatchId));
        }
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
}
