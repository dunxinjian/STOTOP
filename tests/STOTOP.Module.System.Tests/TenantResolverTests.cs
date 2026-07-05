using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services;
using Xunit;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 缺陷4 修复：ITenantResolver.ResolveTenantForOrg 按【组织所属租户】(SYS组织架构.F租户ID)解析，
/// 而非固定根租户——使批次链(导入/自动凭证)在多租户上线后按批次组织落到正确租户。
/// InMemory 不支持 GetRootTenantId 的原生 SQL，故此处只覆盖"org 已物化 F租户ID>0"的主路径
/// (正是多租户就绪的核心：不同组织解析到各自租户)；F租户ID&lt;=0/orgId&lt;=0/查不到 → 回退根 的兜底
/// 靠代码走查 + 单租户不变式(见第二个用例)保证。
/// </summary>
public class TenantResolverTests
{
    // 最小 IServiceScopeFactory 假实现：任意 scope 都返回同一个 InMemory 测试 DbContext。
    private sealed class FakeScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        private readonly STOTOPDbContext _ctx;
        public FakeScopeFactory(STOTOPDbContext ctx) => _ctx = ctx;
        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => this;
        public object? GetService(Type serviceType) => serviceType == typeof(STOTOPDbContext) ? _ctx : null;
        public void Dispose() { }
    }

    private static void AddOrg(STOTOPDbContext ctx, long id, long parentId, long tenantId)
    {
        ctx.Set<SysOrganization>().Add(new SysOrganization
        {
            FID = id, FUID = $"u{id}", FName = $"O{id}", FCode = $"C{id}",
            FParentId = parentId, FTenantId = tenantId, FTypeId = 5, FStatus = 1,
        });
    }

    [Fact]
    public void 按组织解析到该组织自身的租户_不同组织解析到不同租户()
    {
        var ctx = TestDbContextFactory.Create(nameof(按组织解析到该组织自身的租户_不同组织解析到不同租户));
        // 模拟多租户上线后：两个区域公司各自成租户根，其子树 F租户ID=各自租户
        AddOrg(ctx, 1, 0, 1);       // 集团根
        AddOrg(ctx, 2, 1, 2);       // 区域公司A(租户2) —— 已物化 F租户ID=2
        AddOrg(ctx, 3, 2, 2);       // A 的子部门 —— 随租户A
        AddOrg(ctx, 192, 1, 192);   // 区域公司B(租户192)
        AddOrg(ctx, 194, 192, 192); // B 的网点公司 —— 随租户B
        ctx.SaveChanges();

        var resolver = new TenantResolver(new FakeScopeFactory(ctx));

        Assert.Equal(2L, resolver.ResolveTenantForOrg(2));     // 区域公司A → 租户2
        Assert.Equal(2L, resolver.ResolveTenantForOrg(3));     // A 子部门 → 租户2(继承)
        Assert.Equal(192L, resolver.ResolveTenantForOrg(192)); // 区域公司B → 租户192
        Assert.Equal(192L, resolver.ResolveTenantForOrg(194)); // B 网点公司 → 租户192(不再错落到根/A)
    }

    [Fact]
    public void 单租户不变式_所有组织F租户ID都等于根_解析恒为根()
    {
        var ctx = TestDbContextFactory.Create(nameof(单租户不变式_所有组织F租户ID都等于根_解析恒为根));
        // 现网单客户：全部 org(含根)F租户ID=组织树根 FID=1
        AddOrg(ctx, 1, 0, 1);
        AddOrg(ctx, 2, 1, 1);
        AddOrg(ctx, 192, 1, 1);
        ctx.SaveChanges();

        var resolver = new TenantResolver(new FakeScopeFactory(ctx));

        // 任意组织都解析到根租户 1——与旧 GetRootTenantId() 行为一致(行为不变)
        Assert.Equal(1L, resolver.ResolveTenantForOrg(2));
        Assert.Equal(1L, resolver.ResolveTenantForOrg(192));
        Assert.Equal(1L, resolver.ResolveTenantForOrg(1));
    }
}
