using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.System.Entities;
using STOTOP.Module.Finance.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Smoke;

[Collection("StotopRealDb")] // 串行化：与其它真库集成测试共享 stotop，避免并行竞态（默认 Skip，但加入以防开启）
public class SqlServerSchemaSmokeTests
{
    private static string? Conn => Environment.GetEnvironmentVariable("STOTOP_TEST_CONNECTION");

    [SkippableFact]
    public async global::System.Threading.Tasks.Task EnsureCreated_Builds_FIsTemplate_Column()
    {
        Skip.If(string.IsNullOrWhiteSpace(Conn), "未设 STOTOP_TEST_CONNECTION，跳过 SQL Server 冒烟");

        STOTOPDbContext.RegisterModuleAssembly(typeof(CfCard).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(SysUser).Assembly);
        STOTOPDbContext.RegisterModuleAssembly(typeof(FinVoucher).Assembly);

        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseSqlServer(Conn)
            .Options;
        await using var db = new STOTOPDbContext(options);

        await db.Database.EnsureCreatedAsync();

        var exists = await db.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'CF卡片流程' AND COLUMN_NAME = N'F是否模板'")
            .SingleAsync();

        Assert.True(exists > 0, "建库后 [CF卡片流程] 应包含 [F是否模板] 列");
    }
}
