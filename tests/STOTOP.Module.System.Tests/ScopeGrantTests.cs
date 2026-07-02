using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services;
using Xunit;
using STT = System.Threading.Tasks;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段2D(R8) 数据范围引擎自检：从任职物化范围根派生授权(4级)、可视节点集(闭包展开+租户二次夹逼)、
/// ApplyVisibilityScope 收窄、(Write,集团) 二人复核门禁、无授权 fail-closed。
/// </summary>
public class ScopeGrantTests
{
    private sealed record OrgRow(long FOrgId) : IOrgScoped { public long FOrgId { get; set; } = FOrgId; }

    private static void AddOrg(STOTOPDbContext ctx, long id, long parentId, OrgKind kind, string name)
        => ctx.Set<SysOrganization>().Add(new SysOrganization
        { FID = id, FUID = $"u{id}", FName = name, FCode = $"C{id}", FParentId = parentId, FKind = (int)kind, FTypeId = 5, FStatus = 1 });

    private static void AddAppointment(STOTOPDbContext ctx, long userId, long orgId, long memberSeed)
    {
        var m = new SysTenantMember { FID = memberSeed, FUserId = userId, FTenantId = 1, FIsPrimary = true, FInviteStatus = 2, FStatus = 1 };
        ctx.Set<SysTenantMember>().Add(m);
        ctx.Set<SysAppointment>().Add(new SysAppointment
        { FTenantId = 1, FMemberId = memberSeed, FOrgId = orgId, FIsPrimary = true, FScopeEligible = true, FIsCurrent = true, FStatus = 1 });
    }

    /// <summary>租户1(MDSTO) 树 + 一个租户2 树(测二次夹逼);建用户任职;物化;返回 (ctx, service)。</summary>
    private static (STOTOPDbContext ctx, ScopeGrantService svc) Build(string db)
    {
        var ctx = TestDbContextFactory.Create(db);
        // 租户1
        AddOrg(ctx, 1, 0, OrgKind.Group, "MDSTO");
        AddOrg(ctx, 2, 1, OrgKind.Region, "石家庄申通");
        AddOrg(ctx, 3, 2, OrgKind.Dept, "业务一中心");
        AddOrg(ctx, 192, 1, OrgKind.Region, "太仓美申");
        AddOrg(ctx, 194, 192, OrgKind.Company, "城区公司");
        AddOrg(ctx, 197, 194, OrgKind.Dept, "洋沙承包区");
        AddOrg(ctx, 251, 1, OrgKind.Dept, "集团大市场中心");
        // 租户2（另一客户，测租户二次夹逼）
        AddOrg(ctx, 500, 0, OrgKind.Group, "他客户集团");
        AddOrg(ctx, 501, 500, OrgKind.Dept, "他客户部门");
        // 任职
        AddAppointment(ctx, 100, 197, 1001); // 承包区 → 范围根=城区公司(Company)
        AddAppointment(ctx, 200, 3, 1002);   // 石家庄申通部门 → 范围根=石家庄申通(Region)
        AddAppointment(ctx, 300, 251, 1003); // 集团直属部门 → 范围根=MDSTO(集团)
        ctx.SaveChanges();

        OrgTreeMaterializer.RebuildAll(ctx);
        return (ctx, new ScopeGrantService(ctx));
    }

    [Fact]
    public async STT.Task 派生授权_按范围根4级_并集团归一()
    {
        var (ctx, svc) = Build("scope_derive");
        using (ctx)
        {
            await svc.RecomputeScopeGrantsAsync(100, 1);
            await svc.RecomputeScopeGrantsAsync(200, 1);
            await svc.RecomputeScopeGrantsAsync(300, 1);

            var g100 = ctx.Set<SysScopeGrant>().IgnoreQueryFilters().Single(g => g.FUserId == 100);
            Assert.Equal((int)OrgScopeType.Company, g100.FScopeType);
            Assert.Equal(194L, g100.FScopeNodeId);

            var g200 = ctx.Set<SysScopeGrant>().IgnoreQueryFilters().Single(g => g.FUserId == 200);
            Assert.Equal((int)OrgScopeType.Region, g200.FScopeType);
            Assert.Equal(2L, g200.FScopeNodeId);

            var g300 = ctx.Set<SysScopeGrant>().IgnoreQueryFilters().Single(g => g.FUserId == 300);
            Assert.Equal((int)OrgScopeType.Group, g300.FScopeType);
            Assert.Equal(1L, g300.FScopeNodeId);
        }
    }

    [Fact]
    public async STT.Task 可视集_网点公司级_仅本公司子树()
    {
        var (ctx, svc) = Build("scope_company");
        using (ctx)
        {
            await svc.RecomputeScopeGrantsAsync(100, 1);
            var visible = await svc.GetVisibleNodeIdsAsync(100, 1, ScopeAction.Read);
            Assert.Equal(new HashSet<long> { 194, 197 }, visible.ToHashSet());
        }
    }

    [Fact]
    public async STT.Task 可视集_区域公司级_仅本区域子树_且用户间互不可见()
    {
        var (ctx, svc) = Build("scope_region");
        using (ctx)
        {
            await svc.RecomputeScopeGrantsAsync(100, 1);
            await svc.RecomputeScopeGrantsAsync(200, 1);
            var v200 = (await svc.GetVisibleNodeIdsAsync(200, 1, ScopeAction.Read)).ToHashSet();
            Assert.Equal(new HashSet<long> { 2, 3 }, v200);
            // 太仓美申侧节点不在石家庄用户可视集
            Assert.DoesNotContain(194L, v200);
            Assert.DoesNotContain(197L, v200);
        }
    }

    [Fact]
    public async STT.Task 可视集_集团级_整租户树_且租户二次夹逼排除他租户()
    {
        var (ctx, svc) = Build("scope_group_clamp");
        using (ctx)
        {
            await svc.RecomputeScopeGrantsAsync(300, 1);
            var visible = (await svc.GetVisibleNodeIdsAsync(300, 1, ScopeAction.Read)).ToHashSet();
            // 整棵租户1树
            Assert.Equal(new HashSet<long> { 1, 2, 3, 192, 194, 197, 251 }, visible);
            // 他租户(500/501)被二次夹逼排除
            Assert.DoesNotContain(500L, visible);
            Assert.DoesNotContain(501L, visible);
        }
    }

    [Fact]
    public async STT.Task 无授权_failclosed_空集()
    {
        var (ctx, svc) = Build("scope_failclosed");
        using (ctx)
        {
            // 用户 999 无任职/授权
            await svc.RecomputeScopeGrantsAsync(999, 1);
            var visible = await svc.GetVisibleNodeIdsAsync(999, 1, ScopeAction.Read);
            Assert.Empty(visible);
        }
    }

    [Fact]
    public void ApplyVisibilityScope_收窄到可视节点()
    {
        var rows = new List<OrgRow> { new(194), new(197), new(2), new(3) }.AsQueryable();
        var visible = new List<long> { 194, 197 };
        var result = rows.ApplyVisibilityScope(visible).Select(r => r.FOrgId).ToHashSet();
        Assert.Equal(new HashSet<long> { 194, 197 }, result);
    }

    [Fact]
    public async STT.Task 手工授权_写集团_无审批单_拒()
    {
        var (ctx, svc) = Build("scope_manual");
        using (ctx)
        {
            var grant = new SysScopeGrant { FUserId = 100, FTenantId = 1, FScopeType = (int)OrgScopeType.Group, FScopeNodeId = 1, FScopeAction = (int)ScopeAction.Write };
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AddManualGrantAsync(grant));

            // 挂审批单则放行
            var ok = new SysScopeGrant { FUserId = 100, FTenantId = 1, FScopeType = (int)OrgScopeType.Group, FScopeNodeId = 1, FScopeAction = (int)ScopeAction.Write, FApprovalId = 555 };
            await svc.AddManualGrantAsync(ok);
            Assert.True(ctx.Set<SysScopeGrant>().IgnoreQueryFilters().Any(g => g.FApprovalId == 555 && g.FGrantSource == (int)ScopeGrantSource.Manual));
        }
    }
}
