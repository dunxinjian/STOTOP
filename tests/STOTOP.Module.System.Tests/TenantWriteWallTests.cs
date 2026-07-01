using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// v2 多租户 fail-closed 【写硬墙】自检（阶段1）：无租户上下文写 ITenantScoped → throw；
/// 跨租户写(FTenantId 指向他租户) → throw；平台作用域写 → 放行。配套 Finance 的读硬墙自检。
/// 载体用 SysFeedbackCard（BaseEntity, IOrgScoped, ITenantScoped）；组织维度置空以聚焦租户维度。
/// </summary>
public class TenantWriteWallTests
{
    private static DbContextOptions<STOTOPDbContext> SharedDb()
    {
        TenantTestModules.RegisterAll();
        return new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseInMemoryDatabase($"tenant_writewall_{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
    }

    private static STOTOPDbContext Ctx(DbContextOptions<STOTOPDbContext> options, long? tenantId, bool platform = false)
        => new(options, new TestDbContextFactory.TestContextAccessor { CurrentOrgId = null, CurrentTenantId = tenantId, IsPlatformScope = platform });

    private static SysFeedbackCard NewCard(long tenantId = 0) =>
        new() { FTenantId = tenantId, FTitle = "反馈", FModule = "test", FSubmitterId = 1 };

    [Fact]
    public async global::System.Threading.Tasks.Task 写硬墙_无租户上下文写入被拒()
    {
        var options = SharedDb();
        using var ctx = Ctx(options, tenantId: null);
        ctx.Set<SysFeedbackCard>().Add(NewCard());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
        Assert.Contains("无租户上下文", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 写硬墙_跨租户写入被拒()
    {
        var options = SharedDb();
        using var ctx = Ctx(options, tenantId: 10);
        ctx.Set<SysFeedbackCard>().Add(NewCard(tenantId: 999)); // 显式指向他租户
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
        Assert.Contains("跨租户", ex.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 写硬墙_有租户上下文自动回填并可见_平台作用域放行()
    {
        var options = SharedDb();

        // 租户 A=10 写入（FTenantId=0 → 自动回填 10）
        using (var a = Ctx(options, tenantId: 10))
        {
            a.Set<SysFeedbackCard>().Add(NewCard());
            await a.SaveChangesAsync();
        }

        // 租户 B=20 读 → 空
        using (var b = Ctx(options, tenantId: 20))
            Assert.Empty(await b.Set<SysFeedbackCard>().ToListAsync());

        // 无上下文读 → 空（fail-closed）
        using (var none = Ctx(options, tenantId: null))
            Assert.Empty(await none.Set<SysFeedbackCard>().ToListAsync());

        // 切回 A=10 → 可见且回填正确
        using (var a2 = Ctx(options, tenantId: 10))
        {
            var list = await a2.Set<SysFeedbackCard>().ToListAsync();
            Assert.Single(list);
            Assert.Equal(10, list[0].FTenantId);
        }

        // 平台作用域 → 放行（跨租户可见全部）
        using (var plat = Ctx(options, tenantId: null, platform: true))
            Assert.Single(await plat.Set<SysFeedbackCard>().ToListAsync());
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 写硬墙_平台作用域写入不抛()
    {
        var options = SharedDb();
        using var ctx = Ctx(options, tenantId: null, platform: true);
        ctx.Set<SysFeedbackCard>().Add(NewCard());
        var affected = await ctx.SaveChangesAsync();
        Assert.Equal(1, affected);
    }
}
