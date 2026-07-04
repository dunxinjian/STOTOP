using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services;
using STOTOP.Module.System.Services.Interfaces;
using Xunit;
using STT = System.Threading.Tasks;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段4D M8 IdP 自检：IDP 四表形态（平台级 vs ITenantScoped）+ IdpService（外部企业/身份 upsert、
/// 免登多租户消歧 428、成员邀请流）。SYS租户成员/IDP身份 均 InMemory 可验。
/// </summary>
public class IdpServiceTests
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

    private static IdpService MakeIdp(STOTOPDbContext ctx)
    {
        var orgContext = new OrgContextService(ctx, new HttpContextAccessor(), new FakeChangeLog(),
            NullLogger<OrgContextService>.Instance, new FakeAdminAuth(),
            new TestDbContextFactory.TestContextAccessor { CurrentTenantId = 1 }, new ScopeGrantService(ctx));
        return new IdpService(ctx, orgContext, new ScopeGrantService(ctx));
    }

    private static void AddOrg(STOTOPDbContext ctx, long id, string name)
        => ctx.Set<SysOrganization>().Add(new SysOrganization { FID = id, FUID = $"u{id}", FName = name, FCode = $"C{id}", FParentId = id == 1 ? 0 : 1, FKind = id == 1 ? 0 : 1, FTypeId = 5, FStatus = 1 });

    private static void AddMember(STOTOPDbContext ctx, long userId, long tenantId, bool primary)
        => ctx.Set<SysTenantMember>().Add(new SysTenantMember { FUserId = userId, FTenantId = tenantId, FInviteStatus = 2, FStatus = 1, FIsPrimary = primary });

    // ---- 模型形态 ----

    [Fact]
    public void IDP四表_平台级与租户级归属正确()
    {
        using var ctx = TestDbContextFactory.Create("idp");
        var model = ctx.Model;

        // 平台级：无租户过滤器、无 FOrgId
        Assert.False(typeof(ITenantScoped).IsAssignableFrom(typeof(IdpExternalCorp)));
        Assert.False(typeof(ITenantScoped).IsAssignableFrom(typeof(IdpUserIdentity)));
        Assert.Null(model.FindEntityType(typeof(IdpExternalCorp))!.FindProperty("FOrgId"));

        // 租户级：进硬墙
        Assert.True(typeof(ITenantScoped).IsAssignableFrom(typeof(IdpTenantCorpMap)));
        Assert.True(typeof(ITenantScoped).IsAssignableFrom(typeof(IdpDeptMap)));

        // IdpDeptMap 有 FOrgId(映射目标列) 但【不】实现 IOrgScoped（不被组织过滤器收窄）；因 ITenantScoped 故漏标门禁不触发
        Assert.NotNull(model.FindEntityType(typeof(IdpDeptMap))!.FindProperty("FOrgId"));
        Assert.False(typeof(IOrgScoped).IsAssignableFrom(typeof(IdpDeptMap)));

        Assert.Equal("IDP外部企业", model.FindEntityType(typeof(IdpExternalCorp))!.GetTableName());
        Assert.Equal("IDP用户身份", model.FindEntityType(typeof(IdpUserIdentity))!.GetTableName());
    }

    // ---- 外部企业 / 用户身份 ----

    [Fact]
    public async STT.Task 外部企业与用户身份_upsert幂等_可反查用户()
    {
        using var ctx = TestDbContextFactory.Create("idp");
        var idp = MakeIdp(ctx);

        await idp.EnsureExternalCorpAsync(IdpProvider.DingTalk, "corpA", "钉钉甲");
        await idp.EnsureExternalCorpAsync(IdpProvider.DingTalk, "corpA", "钉钉甲改名");   // 幂等更新
        Assert.Single(ctx.Set<IdpExternalCorp>());
        Assert.Equal("钉钉甲改名", ctx.Set<IdpExternalCorp>().Single().FName);

        await idp.UpsertUserIdentityAsync(700, "corpA", "ext-700", "union-700");
        await idp.UpsertUserIdentityAsync(700, "corpA", "ext-700b", null);   // 同 (用户,corp) 幂等更新
        Assert.Single(ctx.Set<IdpUserIdentity>().Where(i => i.FUserId == 700));

        Assert.Equal(700L, await idp.ResolveUserByExternalAsync("corpA", "ext-700b"));
        Assert.Null(await idp.ResolveUserByExternalAsync("corpA", "unknown"));
    }

    // ---- 免登多租户消歧 428 ----

    [Fact]
    public async STT.Task 免登消歧_唯一自动_多有主取主_多无主须选()
    {
        using var ctx = TestDbContextFactory.Create("idp");
        AddOrg(ctx, 1, "MDSTO"); AddOrg(ctx, 2, "太仓美申");
        AddMember(ctx, 501, 1, primary: false);                          // 唯一租户
        AddMember(ctx, 502, 1, primary: true); AddMember(ctx, 502, 2, primary: false); // 多个有主
        AddMember(ctx, 503, 1, primary: false); AddMember(ctx, 503, 2, primary: false); // 多个无主
        await ctx.SaveChangesAsync();
        var idp = MakeIdp(ctx);

        var none = await idp.ResolveLoginTenantAsync(500);
        Assert.Null(none.AutoTenantId); Assert.False(none.MustSelect);

        var one = await idp.ResolveLoginTenantAsync(501);
        Assert.Equal(1L, one.AutoTenantId); Assert.False(one.MustSelect);

        var multiPrimary = await idp.ResolveLoginTenantAsync(502);
        Assert.Equal(1L, multiPrimary.AutoTenantId); Assert.False(multiPrimary.MustSelect);

        var multiNoPrimary = await idp.ResolveLoginTenantAsync(503);
        Assert.Null(multiNoPrimary.AutoTenantId);
        Assert.True(multiNoPrimary.MustSelect);          // → HTTP 428
        Assert.Equal(2, multiNoPrimary.Tenants.Count);
    }

    // ---- 成员邀请流 ----

    [Fact]
    public async STT.Task 邀请流_待确认_接受成为成员_再邀请被拒()
    {
        using var ctx = TestDbContextFactory.Create("idp");
        AddOrg(ctx, 1, "MDSTO");
        await ctx.SaveChangesAsync();
        var idp = MakeIdp(ctx);

        await idp.InviteMemberAsync(inviterUserId: 1, targetUserId: 600, tenantId: 1, isPrimary: false);
        var pending = await idp.GetPendingInvitesAsync(600);
        Assert.Single(pending);
        Assert.Equal("MDSTO", pending[0].TenantName);
        Assert.Equal(1L, pending[0].InvitedBy);

        await idp.AcceptInviteAsync(600, 1);
        var m = ctx.Set<SysTenantMember>().Single(x => x.FUserId == 600 && x.FTenantId == 1);
        Assert.Equal(2, m.FInviteStatus);           // 已接受
        Assert.NotNull(m.FJoinedAt);
        Assert.Empty(await idp.GetPendingInvitesAsync(600));  // 不再待确认

        // 已是成员 → 再邀请被拒
        await Assert.ThrowsAsync<InvalidOperationException>(() => idp.InviteMemberAsync(1, 600, 1, false));
    }

    [Fact]
    public async STT.Task 邀请流_拒绝()
    {
        using var ctx = TestDbContextFactory.Create("idp");
        AddOrg(ctx, 2, "太仓美申");
        await ctx.SaveChangesAsync();
        var idp = MakeIdp(ctx);

        await idp.InviteMemberAsync(1, 601, 2, false);
        await idp.RejectInviteAsync(601, 2);
        var m = ctx.Set<SysTenantMember>().Single(x => x.FUserId == 601 && x.FTenantId == 2);
        Assert.Equal(3, m.FInviteStatus);           // 已拒绝
        Assert.Empty(await idp.GetPendingInvitesAsync(601));

        // 已拒绝 → 不能直接接受
        await Assert.ThrowsAsync<InvalidOperationException>(() => idp.AcceptInviteAsync(601, 2));
    }
}
