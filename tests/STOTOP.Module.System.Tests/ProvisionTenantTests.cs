using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services;
using Xunit;
using STT = System.Threading.Tasks;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// R5 新租户自动开通自检：开通后结构完整（组织根/闭包/PLT租户/私有admin角色/管理员用户/成员/主任职/R8授权），
/// 不变量 租户ID=根组织FID=PLT租户.FID，两租户互不串；以及 R5-B 的 admin 判定改为"持 F是否管理员=1"。
/// </summary>
public class ProvisionTenantTests
{
    /// <summary>平台作用域下的 InMemory 上下文（镜像 PlatformController 的 [PlatformOnly]）+ 供组织根用的区域公司类型。</summary>
    private static (STOTOPDbContext ctx, ProvisionTenantService svc) Build(string db)
    {
        TenantTestModules.RegisterAll();
        var accessor = new TestDbContextFactory.TestContextAccessor { IsPlatformScope = true, CurrentTenantId = null };
        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseInMemoryDatabase($"{db}_{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
        var ctx = new STOTOPDbContext(options, accessor);
        // 区域公司(FKind=1) 组织类型——建根节点须能按 FKind 找到类型
        ctx.Set<SysOrgType>().Add(new SysOrgType { FID = 2, FCode = "REGION", FName = "区域公司", FKind = (int)OrgKind.Region });
        ctx.SaveChanges();

        var svc = new ProvisionTenantService(
            ctx,
            new ScopeGrantService(ctx),
            new TenantScopeFactory(accessor, NullLogger<TenantScopeFactory>.Instance),
            NullLogger<ProvisionTenantService>.Instance);
        return (ctx, svc);
    }

    private static ProvisionTenantRequest Req(string code, string admin) => new()
    {
        Name = $"租户{code}",
        Code = code,
        RootOrgName = $"租户{code}",
        RootOrgKind = (int)OrgKind.Region,
        AdminAccount = admin,
        AdminName = "管理员",
        AccountSetBindMode = 1,
        DefaultTodoChannel = 1,
    };

    [Fact]
    public async STT.Task 开通新租户_结构完整_不变量成立()
    {
        var (ctx, svc) = Build("provision_ok");
        using (ctx)
        {
            var r = await svc.ProvisionAsync(Req("TCMS", "tcms_admin"));

            // 不变量：租户ID = 根组织FID = PLT租户.FID
            Assert.True(r.TenantId > 0);
            Assert.Equal(r.TenantId, r.RootOrgId);
            var tid = r.TenantId;

            // 根组织 + 物化
            var root = ctx.Set<SysOrganization>().IgnoreQueryFilters().Single(o => o.FID == tid);
            Assert.Equal(0, root.FParentId);
            Assert.Equal((int)OrgKind.Region, root.FKind);
            Assert.Equal(tid, root.FTenantId); // 物化：本树 F租户ID=自身

            // 闭包自反行
            Assert.True(ctx.Set<SysOrgClosure>().IgnoreQueryFilters()
                .Any(c => c.FAncestorId == tid && c.FDescendantId == tid && c.FDepth == 0));

            // PLT租户 FID==根组织FID
            var plt = ctx.Set<PltTenant>().Single(t => t.FID == tid);
            Assert.Equal(tid, plt.FRootOrgId);
            Assert.Equal((int)PltTenantStatus.Trial, plt.FStatus);

            // 管理员用户（随机密码可验、非平台超管）
            var user = ctx.Set<SysUser>().Single(u => u.FID == r.AdminUserId);
            Assert.Equal("tcms_admin", user.FAccount);
            Assert.False(user.FIsPlatformAdmin);
            Assert.False(string.IsNullOrWhiteSpace(r.TempPassword));
            Assert.True(BCrypt.Net.BCrypt.Verify(r.TempPassword, user.FPasswordHash));

            // 租户私有 admin 角色
            var role = ctx.Set<SysRole>().Single(x => x.FID == r.AdminRoleId);
            Assert.Equal(SysRoleScope.Tenant, role.FScope);
            Assert.Equal(tid, role.FTenantId);
            Assert.True(role.FIsAdmin);
            Assert.True(ctx.Set<SysUserRole>().Any(ur => ur.FUserId == user.FID && ur.FRoleId == role.FID));

            // 成员(已接受/主) + 主任职(当前) + 主组织
            var member = ctx.Set<SysTenantMember>().Single(m => m.FUserId == user.FID && m.FTenantId == tid);
            Assert.Equal(2, member.FInviteStatus);
            Assert.True(member.FIsPrimary);
            Assert.True(ctx.Set<SysUserOrganization>().Any(uo => uo.FUserId == user.FID && uo.FOrgId == tid && uo.F是否当前));
            Assert.True(ctx.Set<SysAppointment>().IgnoreQueryFilters()
                .Any(a => a.FMemberId == member.FID && a.FTenantId == tid && a.FScopeEligible && a.FIsCurrent));

            // R8 派生授权：Read，指向租户根（GetVisibleNodeIds 即整树）
            var grant = ctx.Set<SysScopeGrant>().IgnoreQueryFilters().Single(g => g.FUserId == user.FID && g.FTenantId == tid);
            Assert.Equal((int)ScopeAction.Read, grant.FScopeAction);
            Assert.Equal(tid, grant.FScopeNodeId);
        }
    }

    [Fact]
    public async STT.Task 开通两租户_互不串()
    {
        var (ctx, svc) = Build("provision_two");
        using (ctx)
        {
            var a = await svc.ProvisionAsync(Req("AAA", "aaa_admin"));
            var b = await svc.ProvisionAsync(Req("BBB", "bbb_admin"));

            Assert.NotEqual(a.TenantId, b.TenantId);

            // 各自组织 F租户ID = 自身根，互不覆盖
            var oa = ctx.Set<SysOrganization>().IgnoreQueryFilters().Single(o => o.FID == a.TenantId);
            var ob = ctx.Set<SysOrganization>().IgnoreQueryFilters().Single(o => o.FID == b.TenantId);
            Assert.Equal(a.TenantId, oa.FTenantId);
            Assert.Equal(b.TenantId, ob.FTenantId);

            // 各自私有 admin 角色绑各自租户
            Assert.Equal(a.TenantId, ctx.Set<SysRole>().Single(r => r.FID == a.AdminRoleId).FTenantId);
            Assert.Equal(b.TenantId, ctx.Set<SysRole>().Single(r => r.FID == b.AdminRoleId).FTenantId);

            // A 管理员的 R8 授权只指向 A 根
            var ga = ctx.Set<SysScopeGrant>().IgnoreQueryFilters().Single(g => g.FUserId == a.AdminUserId);
            Assert.Equal(a.TenantId, ga.FScopeNodeId);
            Assert.Equal(a.TenantId, ga.FTenantId);
        }
    }

    [Fact]
    public async STT.Task 开通_租户编号重复_拒()
    {
        var (ctx, svc) = Build("provision_dup");
        using (ctx)
        {
            await svc.ProvisionAsync(Req("DUP", "dup_admin1"));
            // 同编号再开通 → 抛（管理员账号/组织编码也会连带唯一冲突，任一命中即拒）
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ProvisionAsync(Req("DUP", "dup_admin2")));
        }
    }

    [Fact]
    public async STT.Task 开通_非法根类别_拒()
    {
        var (ctx, svc) = Build("provision_badkind");
        using (ctx)
        {
            var req = Req("BAD", "bad_admin");
            req.RootOrgKind = (int)OrgKind.Dept; // 部门不是合法根类别
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ProvisionAsync(req));
        }
    }

    [Fact]
    public async STT.Task 管理员判定_认F是否管理员_含租户私有角色()
    {
        var ctx = TestDbContextFactory.Create("admin_recognition");
        using (ctx)
        {
            ctx.Set<SysRole>().Add(new SysRole { FID = 1, FName = "超级管理员", FCode = "ADMIN", FScope = SysRoleScope.Platform, FIsAdmin = true });
            ctx.Set<SysRole>().Add(new SysRole { FID = 50, FName = "租户管理员", FCode = "TENANT_ADMIN_X", FScope = SysRoleScope.Tenant, FTenantId = 10, FIsAdmin = true });
            ctx.Set<SysRole>().Add(new SysRole { FID = 60, FName = "出纳", FCode = "CASHIER", FScope = SysRoleScope.Platform, FIsAdmin = false });
            ctx.Set<SysUserRole>().Add(new SysUserRole { FUserId = 1, FRoleId = 1 });   // 平台 admin
            ctx.Set<SysUserRole>().Add(new SysUserRole { FUserId = 2, FRoleId = 50 });  // 租户私有 admin
            ctx.Set<SysUserRole>().Add(new SysUserRole { FUserId = 3, FRoleId = 60 });  // 普通角色
            ctx.SaveChanges();

            var svc = new AdminAuthorizationService();
            Assert.True(await svc.IsAdminByUserIdAsync(ctx, 1));   // 平台 admin
            Assert.True(await svc.IsAdminByUserIdAsync(ctx, 2));   // 租户私有 admin 也算 admin
            Assert.False(await svc.IsAdminByUserIdAsync(ctx, 3));  // 普通角色不是 admin
        }
    }
}
