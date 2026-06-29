using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Quality.Entities;
using STOTOP.Module.System.Entities;
using STOTOP.WebAPI.Data.Seeders;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Unify;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL Task 命名空间，恢复别名
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// Plan2 Phase A：统一质控模型建表冒烟（连开发测试库 stotop）。
/// QualityUnifySeeder.EnsureTables 幂等建 5 张表 + 唯一索引；断言 5 表在 sys.tables、
/// 质量事件源行唯一索引 UX_QL申通_质量事件_源行 在 sys.indexes。
/// 建表幂等，不清理表（仅建结构，不写业务数据）。
/// </summary>
[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop
public class QlSchemaSmokeTests
{
    private static STOTOPDbContext CreateDbContext(string conn)
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(CfCard).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(SysUser).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(FinVoucher).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(QlException).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(ExpSalesman).Assembly);

        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseSqlServer(conn)
            .Options;
        return new STOTOPDbContext(options);
    }

    [SkippableFact]
    public async Task EnsureTables_Creates5Tables_And_QualityEventUniqueIndex()
    {
        Skip.IfNot(TestSqlConnection.IsAvailable, "无可用 SQL 连接（未设 STOTOP_TEST_CONNECTION 且主树 db-connections.json 不可读），跳过集成测试");

        var conn = TestSqlConnection.GetConnectionString()!;

        await using var db = CreateDbContext(conn);

        // 可达性兜底：连不上则跳过而非失败
        try { await db.Database.OpenConnectionAsync(); await db.Database.CloseConnectionAsync(); }
        catch (Exception ex) { Skip.If(true, $"SQL 不可达，跳过：{ex.Message}"); }

        // ── 建表（幂等）──
        QualityUnifySeeder.EnsureTables(db);

        // ── 断言 5 张表存在 ──
        var tables = new[]
        {
            "QL申通_质量问题字典",
            "QL申通_承运商质量事件",
            "QL申通_员工日质量指标",
            "QL申通_网点日质量指标",
            "EXP快递业务员名称映射",
        };

        foreach (var t in tables)
        {
            var exists = await db.Database
                .SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = {0}", t)
                .SingleAsync();
            Assert.True(exists > 0, $"EnsureTables 后应存在表 [{t}]");
        }

        // ── 断言质量事件源行唯一索引存在 ──
        var idxExists = await db.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE name = N'UX_QL申通_质量事件_源行' AND object_id = OBJECT_ID(N'QL申通_承运商质量事件')")
            .SingleAsync();
        Assert.True(idxExists > 0, "应存在唯一索引 [UX_QL申通_质量事件_源行]");
    }
}
