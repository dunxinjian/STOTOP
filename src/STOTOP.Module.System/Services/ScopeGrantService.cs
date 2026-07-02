using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

/// <summary>
/// R8 数据范围引擎（多租户阶段2D，§7）。派生授权由任职(FScopeEligible)的物化范围根(2A)算出;
/// 可视节点集经 SYS组织闭包(2A) 展开;<see cref="ScopeQueryExtensions.ApplyVisibilityScope"/> 逐查询 opt-in 收窄。
/// 单租户过渡期可视域退化为整棵租户树,当期功能收益近零——本轮只落地基/门禁/试点,不铺开全模块。
/// </summary>
public class ScopeGrantService : IScopeGrantService
{
    private readonly STOTOPDbContext _ctx;

    public ScopeGrantService(STOTOPDbContext ctx) => _ctx = ctx;

    public async Task RecomputeScopeGrantsAsync(long userId, long tenantId)
    {
        // 1. 删旧派生授权（保留手工）
        var old = await _ctx.Set<SysScopeGrant>()
            .Where(g => g.FUserId == userId && g.FTenantId == tenantId && g.FGrantSource == (int)ScopeGrantSource.Derived)
            .ToListAsync();
        if (old.Count > 0) _ctx.Set<SysScopeGrant>().RemoveRange(old);

        // 2. 当前可放大任职 → 组织节点 → 物化范围根
        var scopeRoots = await (
            from a in _ctx.Set<SysAppointment>()
            join m in _ctx.Set<SysTenantMember>() on a.FMemberId equals m.FID
            join o in _ctx.Set<SysOrganization>() on a.FOrgId equals o.FID
            where m.FUserId == userId && a.FTenantId == tenantId && a.FIsCurrent && a.FScopeEligible
            select new { o.FScopeRootId, o.FScopeRootType }
        ).Distinct().ToListAsync();

        // 3. 归一化：有集团(Group=1)级 → 只留集团（吞并所有）
        var roots = scopeRoots.Any(s => s.FScopeRootType == (int)OrgScopeType.Group)
            ? scopeRoots.Where(s => s.FScopeRootType == (int)OrgScopeType.Group).ToList()
            : scopeRoots;

        foreach (var r in roots.DistinctBy(x => x.FScopeRootId))
        {
            _ctx.Set<SysScopeGrant>().Add(new SysScopeGrant
            {
                FUserId = userId,
                FTenantId = tenantId,
                FScopeType = r.FScopeRootType,
                FScopeNodeId = r.FScopeRootId,
                FScopeAction = (int)ScopeAction.Read,
                FGrantSource = (int)ScopeGrantSource.Derived,
                FStatus = 1,
            });
        }

        await _ctx.SaveChangesAsync();
    }

    public async Task<IReadOnlyCollection<long>> GetVisibleNodeIdsAsync(long userId, long tenantId, ScopeAction action)
    {
        var now = DateTime.Now;
        var grants = await _ctx.Set<SysScopeGrant>()
            .Where(g => g.FUserId == userId && g.FTenantId == tenantId && g.FStatus == 1
                     && (g.FExpireAt == null || g.FExpireAt > now)
                     && (g.FScopeAction == (int)ScopeAction.All || g.FScopeAction == (int)action))
            .Select(g => new { g.FScopeType, g.FScopeNodeId })
            .ToListAsync();

        if (grants.Count == 0)
            return Array.Empty<long>(); // fail-closed：无授权 → 空集

        // 集团级 → 整棵租户树
        if (grants.Any(g => g.FScopeType == (int)OrgScopeType.Group))
            return await _ctx.Set<SysOrganization>()
                .IgnoreQueryFilters()
                .Where(o => o.FTenantId == tenantId)
                .Select(o => o.FID)
                .ToListAsync();

        // 否则经闭包展开各范围节点后代子树 + 租户二次夹逼
        var nodeIds = grants.Select(g => g.FScopeNodeId).Distinct().ToList();
        return await (
            from c in _ctx.Set<SysOrgClosure>()
            join o in _ctx.Set<SysOrganization>().IgnoreQueryFilters() on c.FDescendantId equals o.FID
            where nodeIds.Contains(c.FAncestorId) && o.FTenantId == tenantId
            select c.FDescendantId
        ).Distinct().ToListAsync();
    }

    public async Task AddManualGrantAsync(SysScopeGrant grant)
    {
        grant.FGrantSource = (int)ScopeGrantSource.Manual;

        // D6：手工 (Write/All, 集团) 授权须挂二人复核审批单
        var isWrite = grant.FScopeAction == (int)ScopeAction.Write || grant.FScopeAction == (int)ScopeAction.All;
        if (isWrite && grant.FScopeType == (int)OrgScopeType.Group && grant.FApprovalId == null)
            throw new InvalidOperationException("全租户(集团)写授权须挂二人复核审批单(FApprovalId)");

        _ctx.Set<SysScopeGrant>().Add(grant);
        await _ctx.SaveChangesAsync();
    }
}
