using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services;
using STOTOP.Module.System.Services.Interfaces;
using System.Security.Claims;
using Xunit;
// STOTOP.Module.Task 命名空间遮蔽 System.Threading.Tasks.Task（本测试命名空间在 STOTOP.Module 下），故用 STT 别名。
using STT = System.Threading.Tasks;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段2B(M3) 成员/任职拆分自检：O(1) 切换列表(经物化 F可切换根ID，语义同旧 FindSwitchableAncestor)、
/// 增量双写 SYS租户成员 + SYS任职、DingTalk 式调和。
/// </summary>
public class OrgMembershipTests
{
    // ---- 轻量 fakes ----
    private sealed class FakeAdminAuth : IAdminAuthorizationService
    {
        public bool AdminResult;
        public bool IsAdmin(ClaimsPrincipal? user) => AdminResult;
        public STT.Task<bool> IsAdminByUserIdAsync(STOTOPDbContext db, long userId) => STT.Task.FromResult(AdminResult);
    }

    private sealed class FakeChangeLog : IChangeLogService
    {
        public STT.Task LogChangeAsync(string a, long b, string c, string d, string e, long? f, string? g) => STT.Task.CompletedTask;
        public STT.Task<(List<ChangeLogDto> Items, int Total)> GetPagedListAsync(ChangeLogQueryRequest r) => STT.Task.FromResult((new List<ChangeLogDto>(), 0));
        public STT.Task<List<ChangeLogDto>> GetByBusinessAsync(string t, long id) => STT.Task.FromResult(new List<ChangeLogDto>());
        public string CompareAndSerialize<T>(T o, T n, params string[] ex) => "";
    }

    private static OrgContextService MakeService(STOTOPDbContext ctx, bool admin = false)
        => new(ctx, new HttpContextAccessor(), new FakeChangeLog(),
               NullLogger<OrgContextService>.Instance, new FakeAdminAuth { AdminResult = admin },
               new TestDbContextFactory.TestContextAccessor { CurrentTenantId = 1 });

    private static void AddOrg(STOTOPDbContext ctx, long id, long parentId, OrgKind kind, bool switchable, string name)
        => ctx.Set<SysOrganization>().Add(new SysOrganization
        {
            FID = id, FUID = $"u{id}", FName = name, FCode = $"C{id}",
            FParentId = parentId, FKind = (int)kind, FTypeId = 5, FIsSwitchable = switchable, FStatus = 1,
        });

    /// <summary>MDSTO(集团,可切换) → 太仓美申(区域公司,可切换) → 城区公司(网点公司,不可切) → 洋沙承包区(部门,不可切)。</summary>
    private static STOTOPDbContext BuildTree(string db)
    {
        var ctx = TestDbContextFactory.Create(db);
        AddOrg(ctx, 1, 0, OrgKind.Group, true, "MDSTO");
        AddOrg(ctx, 192, 1, OrgKind.Region, true, "太仓美申");
        AddOrg(ctx, 194, 192, OrgKind.Company, false, "城区公司");
        AddOrg(ctx, 197, 194, OrgKind.Dept, false, "洋沙承包区");
        ctx.SaveChanges();
        OrgTreeMaterializer.RebuildAll(ctx);
        return ctx;
    }

    [Fact]
    public void 物化_可切换根_取最近可切换祖先()
    {
        using var ctx = BuildTree("mem_switchroot");
        var byId = ctx.Set<SysOrganization>().IgnoreQueryFilters().ToDictionary(o => o.FID);
        Assert.Equal(1L, byId[1].FSwitchRootId);     // 集团自身可切换
        Assert.Equal(192L, byId[192].FSwitchRootId);  // 区域公司自身可切换
        Assert.Equal(192L, byId[194].FSwitchRootId);  // 网点公司不可切 → 最近可切换=太仓美申
        Assert.Equal(192L, byId[197].FSwitchRootId);  // 承包区 → 太仓美申
    }

    [Fact]
    public async STT.Task 切换列表_O1_取可切换根_语义同旧上溯()
    {
        using var ctx = BuildTree("mem_switchlist");
        // 用户在承包区(197)任职
        ctx.Set<SysUserOrganization>().Add(new SysUserOrganization { FUserId = 100, FOrgId = 197, FIsPrimaryOrg = 1, FStatus = 1, F是否当前 = true });
        ctx.SaveChanges();

        var svc = MakeService(ctx, admin: false);
        var list = await svc.GetUserOrganizationsAsync(100);

        // 切换目标 = 最近可切换祖先 太仓美申(192)，去重后 1 条
        Assert.Single(list);
        Assert.Equal(192L, list[0].OrgId);
        Assert.Equal(192L, list[0].SwitchableOrgId);
    }

    [Fact]
    public async STT.Task 双写_新增任职_落成员与任职()
    {
        using var ctx = BuildTree("mem_dualwrite_add");
        var svc = MakeService(ctx, admin: false);

        await svc.AddUserToOrganizationAsync(new AddUserToOrganizationRequest
        {
            UserId = 100, OrgId = 197, IsPrimaryOrg = 1, Position = "承包区长"
        });

        var member = await ctx.Set<SysTenantMember>().IgnoreQueryFilters().SingleAsync(m => m.FUserId == 100);
        Assert.Equal(1L, member.FTenantId);
        Assert.True(member.FIsPrimary);
        Assert.Equal(2, member.FInviteStatus); // 已接受

        var appt = await ctx.Set<SysAppointment>().IgnoreQueryFilters().SingleAsync(a => a.FMemberId == member.FID);
        Assert.Equal(197L, appt.FOrgId);
        Assert.Equal(1L, appt.FTenantId);
        Assert.True(appt.FIsPrimary);
        Assert.True(appt.FScopeEligible);   // 主任职 → 可参与范围放大
        Assert.True(appt.FIsCurrent);
    }

    [Fact]
    public async STT.Task 双写_删除任职_移除任职()
    {
        using var ctx = BuildTree("mem_dualwrite_remove");
        var svc = MakeService(ctx, admin: false);
        await svc.AddUserToOrganizationAsync(new AddUserToOrganizationRequest { UserId = 100, OrgId = 197, IsPrimaryOrg = 1 });

        var uoId = ctx.Set<SysUserOrganization>().Single(u => u.FUserId == 100 && u.FOrgId == 197).FID;
        await svc.RemoveUserFromOrganizationAsync(uoId);

        Assert.Empty(ctx.Set<SysAppointment>().IgnoreQueryFilters().Where(a => a.FOrgId == 197));
    }

    [Fact]
    public async STT.Task 调和_按当前用户组织重建任职()
    {
        using var ctx = BuildTree("mem_reconcile");
        // 直接造 SYS用户组织(模拟 DingTalk 同步结果)，未经双写
        ctx.Set<SysUserOrganization>().Add(new SysUserOrganization { FUserId = 100, FOrgId = 197, FIsPrimaryOrg = 1, FStatus = 1, F是否当前 = true });
        ctx.Set<SysUserOrganization>().Add(new SysUserOrganization { FUserId = 100, FOrgId = 194, FIsPrimaryOrg = 0, FStatus = 1, F是否当前 = true });
        ctx.SaveChanges();

        var svc = MakeService(ctx, admin: false);
        await svc.ReconcileUserMembershipBestEffortAsync(100);

        var member = ctx.Set<SysTenantMember>().IgnoreQueryFilters().Single(m => m.FUserId == 100);
        var appts = ctx.Set<SysAppointment>().IgnoreQueryFilters().Where(a => a.FMemberId == member.FID).ToList();
        Assert.Equal(2, appts.Count);
        Assert.True(appts.Single(a => a.FOrgId == 197).FScopeEligible);   // 主任职
        Assert.False(appts.Single(a => a.FOrgId == 194).FScopeEligible);  // 非主任职不放大
    }
}
