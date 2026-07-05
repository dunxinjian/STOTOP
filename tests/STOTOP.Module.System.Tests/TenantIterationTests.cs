using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services;
using Xunit;
// System.Tests 引用了 STOTOP.Module.Task 模块 → 裸 Task 被遮蔽为命名空间，用 STT 别名指 System.Threading.Tasks。
using STT = System.Threading.Tasks;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段4 收尾·per-tenant 迭代地基自检：
/// TenantIterationService（枚举活跃租户 / 跳停用 / 冻结照跑 / 单租户回退 / 失败隔离）
/// + TenantScopeFactory（只设 CurrentTenantId、复位为进入前值、绝不放行平台旁路）。
/// </summary>
public class TenantIterationTests
{
    private sealed class FakeTenantResolver : ITenantResolver
    {
        private readonly long? _root;
        public FakeTenantResolver(long? root) => _root = root;
        public long? GetRootTenantId() => _root;
        public long? ResolveTenantForOrg(long orgId) => _root;
    }

    private static (TenantIterationService svc, HttpOrgContextAccessor accessor) BuildService(
        STOTOPDbContext db, long? fallbackRoot = 999)
    {
        // 真实静态-AsyncLocal 访问器：先清残留（跨测试隔离，与既有 Platform* 测试同做法）。
        var accessor = new HttpOrgContextAccessor(new HttpContextAccessor());
        accessor.ClearOverride();
        var scopeFactory = new TenantScopeFactory(accessor, NullLogger<TenantScopeFactory>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        var sp = services.BuildServiceProvider();

        var svc = new TenantIterationService(
            scopeFactory,
            sp.GetRequiredService<IServiceScopeFactory>(),
            new FakeTenantResolver(fallbackRoot),
            NullLogger<TenantIterationService>.Instance);
        return (svc, accessor);
    }

    private static void SeedTenants(STOTOPDbContext db, params (long id, PltTenantStatus st)[] rows)
    {
        foreach (var (id, st) in rows)
            db.Set<PltTenant>().Add(new PltTenant { FID = id, FName = $"T{id}", FCode = $"T{id}", FStatus = (int)st });
        db.SaveChanges();
    }

    [Fact]
    public async STT.Task 迭代覆盖活跃租户_跳停用_含冻结_每租户上下文正确且事后复位()
    {
        using var db = TestDbContextFactory.Create("tenant_iter");
        SeedTenants(db,
            (10, PltTenantStatus.Trial),
            (20, PltTenantStatus.Active),
            (30, PltTenantStatus.Disabled),  // 停用 → 应跳过
            (40, PltTenantStatus.Frozen));   // 冻结 → 照跑（D7）
        var (svc, accessor) = BuildService(db);

        var visited = new List<long>();
        var ctxDuring = new List<long?>();
        await svc.ForEachActiveTenantAsync(tid =>
        {
            visited.Add(tid);
            ctxDuring.Add(accessor.CurrentTenantId);
            return STT.Task.CompletedTask;
        }, "unit-test");

        Assert.Equal(new long[] { 10, 20, 40 }, visited);      // 排除停用(30)、含冻结(40)、按 FID 升序
        Assert.Equal(new long?[] { 10, 20, 40 }, ctxDuring);   // 每次上下文=对应租户
        Assert.Null(accessor.CurrentTenantId);                 // 迭代后复位（进入前为 null）

        accessor.ClearOverride();
    }

    [Fact]
    public async STT.Task PLT租户空表_回退单租户_只跑一次()
    {
        using var db = TestDbContextFactory.Create("tenant_iter_empty");
        // 不 seed 任何 PltTenant → 空表 → 回退 GetRootTenantId()
        var (svc, accessor) = BuildService(db, fallbackRoot: 777);

        var visited = new List<long>();
        await svc.ForEachActiveTenantAsync(tid =>
        {
            visited.Add(tid);
            return STT.Task.CompletedTask;
        }, "unit-test");

        Assert.Equal(new long[] { 777 }, visited);  // 单客户向后兼容：只循环 1 次
        accessor.ClearOverride();
    }

    [Fact]
    public async STT.Task 单租户失败被隔离_其它租户继续_且异常路径也复位()
    {
        using var db = TestDbContextFactory.Create("tenant_iter_iso");
        SeedTenants(db,
            (10, PltTenantStatus.Active),
            (20, PltTenantStatus.Active),
            (30, PltTenantStatus.Active));
        var (svc, accessor) = BuildService(db);

        var visited = new List<long>();
        await svc.ForEachActiveTenantAsync(tid =>
        {
            visited.Add(tid);
            if (tid == 20) throw new InvalidOperationException("boom");
            return STT.Task.CompletedTask;
        }, "unit-test");

        Assert.Equal(new long[] { 10, 20, 30 }, visited);  // 20 抛异常不中断 30
        Assert.Null(accessor.CurrentTenantId);             // 异常经 using 仍复位
        accessor.ClearOverride();
    }

    [Fact]
    public void 租户作用域_嵌套复位为进入前值()
    {
        var accessor = new HttpOrgContextAccessor(new HttpContextAccessor());
        accessor.ClearOverride();
        var f = new TenantScopeFactory(accessor, NullLogger<TenantScopeFactory>.Instance);

        accessor.CurrentTenantId = 5;  // 预设外层上下文
        using (f.Enter(9, "outer"))
        {
            Assert.Equal(9, accessor.CurrentTenantId);
            using (f.Enter(13, "inner"))
                Assert.Equal(13, accessor.CurrentTenantId);
            Assert.Equal(9, accessor.CurrentTenantId);  // 内层退出复位为 9
        }
        Assert.Equal(5, accessor.CurrentTenantId);      // 外层退出复位为 5

        accessor.ClearOverride();
    }

    [Fact]
    public void 租户作用域_只收敛租户_绝不放行平台旁路()
    {
        var accessor = new HttpOrgContextAccessor(new HttpContextAccessor());
        accessor.ClearOverride();
        var f = new TenantScopeFactory(accessor, NullLogger<TenantScopeFactory>.Instance);

        using (f.Enter(9, "x"))
        {
            Assert.Equal(9, accessor.CurrentTenantId);
            Assert.False(accessor.IsPlatformScope);  // 命门：只设租户，绝不置 IsPlatformScope（否则串租户）
        }
        accessor.ClearOverride();
    }
}
