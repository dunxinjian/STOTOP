using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Entities;
using Xunit;

namespace STOTOP.Module.Finance.Tests;

/// <summary>
/// v2 多租户 fail-closed 隔离自检（阶段1a·读硬墙）。同一 InMemory 库内：
/// 租户A 写入的 ITenantScoped 数据，切到租户B / 无租户上下文读不到；切回租户A 可见；平台作用域放行。
/// </summary>
public class TenantIsolationTests
{
    private static DbContextOptions<STOTOPDbContext> SharedDb()
    {
        STOTOPDbContext.RegisterModuleAssembly(typeof(FinVoucher).Assembly);
        return new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseInMemoryDatabase($"tenant_iso_{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
    }

    private static STOTOPDbContext Ctx(DbContextOptions<STOTOPDbContext> options, long? tenantId, bool platform = false)
        => new(options, new TestDbContextFactory.TestContextAccessor { CurrentTenantId = tenantId, IsPlatformScope = platform });

    [Fact]
    public async Task 租户隔离_写A_切B与无上下文读不到_切回A可见_平台放行()
    {
        var options = SharedDb();

        // 租户 A=10 写入一张凭证（FTenantId 由 FillTenantIdForNewEntities 回填为 10）
        using (var a = Ctx(options, 10))
        {
            a.Set<FinVoucher>().Add(new FinVoucher { FVoucherWord = "记", FAccountSetId = 1 });
            await a.SaveChangesAsync();
        }

        // 租户 B=20 读 → 空（跨租户隔离）
        using (var b = Ctx(options, 20))
            Assert.Empty(await b.Set<FinVoucher>().ToListAsync());

        // 无租户上下文(CurrentTenantId=null, 非平台) → 空（fail-closed，不认 null）
        using (var none = Ctx(options, null))
            Assert.Empty(await none.Set<FinVoucher>().ToListAsync());

        // 切回租户 A=10 → 可见自己的数据，且 F租户ID 已回填
        using (var a2 = Ctx(options, 10))
        {
            var list = await a2.Set<FinVoucher>().ToListAsync();
            Assert.Single(list);
            Assert.Equal(10, list[0].FTenantId);
        }

        // 平台作用域 → 放行（跨租户可见全部）
        using (var plat = Ctx(options, null, platform: true))
            Assert.Single(await plat.Set<FinVoucher>().ToListAsync());
    }
}
