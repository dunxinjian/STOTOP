using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services;
using STOTOP.Module.System.Services.Interfaces;
using Xunit;
using STT = System.Threading.Tasks;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段4C M9 后端租户上下文自检：ValidateTenantMembershipAsync（X-Tenant-Context 头校验依据）+ SwitchTenantAsync（R6 租户切换）。
/// SYS租户成员 非 ITenantScoped → LINQ 可 InMemory 验证。中间件的 X-Tenant-Context 加性/403 端到端留集成验证。
/// </summary>
public class TenantContextTests
{
    private sealed class FakeAdminAuth : IAdminAuthorizationService
    {
        public bool IsAdmin(ClaimsPrincipal? user) => false;
        public STT.Task<bool> IsAdminByUserIdAsync(STOTOPDbContext db, long userId) => STT.Task.FromResult(false);
        public STT.Task<bool> IsPlatformAdminByUserIdAsync(STOTOPDbContext db, long userId) => STT.Task.FromResult(false);
    }

    private sealed class FakeChangeLog : IChangeLogService
    {
        public STT.Task LogChangeAsync(string a, long b, string c, string d, string e, long? f, string? g) => STT.Task.CompletedTask;
        public STT.Task<(List<ChangeLogDto> Items, int Total)> GetPagedListAsync(ChangeLogQueryRequest r) => STT.Task.FromResult((new List<ChangeLogDto>(), 0));
        public STT.Task<List<ChangeLogDto>> GetByBusinessAsync(string t, long id) => STT.Task.FromResult(new List<ChangeLogDto>());
        public string CompareAndSerialize<T>(T o, T n, params string[] ex) => "";
    }

    private static OrgContextService MakeService(STOTOPDbContext ctx)
        => new(ctx, new HttpContextAccessor(), new FakeChangeLog(),
               NullLogger<OrgContextService>.Instance, new FakeAdminAuth(),
               new TestDbContextFactory.TestContextAccessor { CurrentTenantId = 1 },
               new ScopeGrantService(ctx));

    private static void AddMember(STOTOPDbContext ctx, long userId, long tenantId, int invite, int status, bool primary = false)
        => ctx.Set<SysTenantMember>().Add(new SysTenantMember
        {
            FUserId = userId, FTenantId = tenantId, FInviteStatus = invite, FStatus = status, FIsPrimary = primary,
        });

    // ---- ValidateTenantMembershipAsync ----

    [Fact]
    public async STT.Task 校验成员_仅认已接受且启用()
    {
        using var ctx = TestDbContextFactory.Create("tenant_ctx");
        AddMember(ctx, 100, 1, invite: 2, status: 1);   // 已接受
        AddMember(ctx, 100, 5, invite: 1, status: 1);   // 待确认
        AddMember(ctx, 100, 6, invite: 3, status: 1);   // 已拒绝
        AddMember(ctx, 100, 7, invite: 2, status: 0);   // 已接受但停用
        await ctx.SaveChangesAsync();
        var svc = MakeService(ctx);

        Assert.True(await svc.ValidateTenantMembershipAsync(100, 1));    // 已接受+启用
        Assert.False(await svc.ValidateTenantMembershipAsync(100, 5));   // 待确认
        Assert.False(await svc.ValidateTenantMembershipAsync(100, 6));   // 已拒绝
        Assert.False(await svc.ValidateTenantMembershipAsync(100, 7));   // 停用
        Assert.False(await svc.ValidateTenantMembershipAsync(100, 999)); // 无该租户成员行（防伪造他租户id）
        Assert.False(await svc.ValidateTenantMembershipAsync(200, 1));   // 非成员用户
    }

    // ---- SwitchTenantAsync ----

    [Fact]
    public async STT.Task 切换租户_非成员被拒()
    {
        using var ctx = TestDbContextFactory.Create("tenant_ctx");
        var svc = MakeService(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SwitchTenantAsync(100, 1));
    }

    [Fact]
    public async STT.Task 切换租户_成员_返回本租户可切换组织并自动选主组织()
    {
        using var ctx = TestDbContextFactory.Create("tenant_ctx");
        // 组织树 MDSTO(1)→太仓美申(192,可切换)→城区(194)→承包区(197)，全 F租户ID=1
        ctx.Set<SysOrganization>().Add(new SysOrganization { FID = 1, FUID = "u1", FName = "MDSTO", FCode = "C1", FParentId = 0, FKind = 0, FTypeId = 5, FIsSwitchable = true, FStatus = 1 });
        ctx.Set<SysOrganization>().Add(new SysOrganization { FID = 192, FUID = "u192", FName = "太仓美申", FCode = "C192", FParentId = 1, FKind = 1, FTypeId = 5, FIsSwitchable = true, FStatus = 1 });
        ctx.Set<SysOrganization>().Add(new SysOrganization { FID = 194, FUID = "u194", FName = "城区公司", FCode = "C194", FParentId = 192, FKind = 2, FTypeId = 5, FIsSwitchable = false, FStatus = 1 });
        ctx.Set<SysOrganization>().Add(new SysOrganization { FID = 197, FUID = "u197", FName = "承包区", FCode = "C197", FParentId = 194, FKind = 4, FTypeId = 5, FIsSwitchable = false, FStatus = 1 });
        ctx.Set<SysUserOrganization>().Add(new SysUserOrganization { FUserId = 100, FOrgId = 197, FIsPrimaryOrg = 1, FStatus = 1, F是否当前 = true });
        AddMember(ctx, 100, 1, invite: 2, status: 1, primary: true);
        await ctx.SaveChangesAsync();
        OrgTreeMaterializer.RebuildAll(ctx);   // 物化 F租户ID/F可切换根ID

        var svc = MakeService(ctx);
        var resp = await svc.SwitchTenantAsync(100, 1);

        Assert.Equal(1L, resp.TenantId);
        Assert.Equal("MDSTO", resp.TenantName);
        Assert.Single(resp.Organizations);               // 可切换根=太仓美申(192)
        Assert.Equal(192L, resp.Organizations[0].OrgId);
        Assert.NotNull(resp.Context);                     // 主组织 → 自动重算上下文
        Assert.Equal(192L, resp.Context!.OrgId);
    }

    [Fact]
    public async STT.Task 切换租户_成员但本租户无组织_返回空组织与空上下文()
    {
        using var ctx = TestDbContextFactory.Create("tenant_ctx");
        // 用户在租户1有组织；但切到其为成员的租户2（无任何 F租户ID=2 的组织）
        ctx.Set<SysOrganization>().Add(new SysOrganization { FID = 1, FUID = "u1", FName = "MDSTO", FCode = "C1", FParentId = 0, FKind = 0, FTypeId = 5, FIsSwitchable = true, FStatus = 1 });
        ctx.Set<SysOrganization>().Add(new SysOrganization { FID = 192, FUID = "u192", FName = "太仓美申", FCode = "C192", FParentId = 1, FKind = 1, FTypeId = 5, FIsSwitchable = true, FStatus = 1 });
        ctx.Set<SysUserOrganization>().Add(new SysUserOrganization { FUserId = 100, FOrgId = 192, FIsPrimaryOrg = 1, FStatus = 1, F是否当前 = true });
        AddMember(ctx, 100, 2, invite: 2, status: 1, primary: true);   // 成员于租户2
        await ctx.SaveChangesAsync();
        OrgTreeMaterializer.RebuildAll(ctx);

        var svc = MakeService(ctx);
        var resp = await svc.SwitchTenantAsync(100, 2);

        Assert.Equal(2L, resp.TenantId);
        Assert.Empty(resp.Organizations);   // 租户2 无用户可切换组织
        Assert.Null(resp.Context);
    }
}
