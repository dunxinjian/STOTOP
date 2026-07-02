using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 组织树物化维护器（M4 多租户阶段2）。从邻接树 SysOrganization.FParentId 物化派生：
/// F父类别 / F所属网点公司ID / F范围根ID/类型 / F路径 / F租户ID，并重建 SYS组织闭包。
/// 合法父子规则(<see cref="IsLegalChild"/>) + 范围根算法(<see cref="ResolveScopeRoot"/>) 也在此，供 OrganizationService 与门禁测试共用。
/// 应用层维护(弃 DB 触发器：本仓零触发器先例 + 触发器与 EF 变更跟踪相互踩坑)；树小(~320 节点)，节点变更后全量重建足够。
/// 注：当前 OrganizationService 建/改/删 与本重建是两次 SaveChanges(非单事务)；重建幂等、可重跑,单客户下风险可控;
/// 并发树编辑的"同事务重算 + FRowVersion 乐观锁"是多客户上线时的硬化点(阶段4)。
/// </summary>
public static class OrgTreeMaterializer
{
    /// <summary>合法 (父类别 → 子类别) 对（放宽：允许 部门→部门 深链，贴合现实组织）。</summary>
    private static readonly Dictionary<int, HashSet<int>> LegalChildKinds = new()
    {
        [(int)OrgKind.Group]   = new() { (int)OrgKind.Region, (int)OrgKind.Center, (int)OrgKind.Dept },
        [(int)OrgKind.Region]  = new() { (int)OrgKind.Center, (int)OrgKind.Company, (int)OrgKind.Dept },
        [(int)OrgKind.Center]  = new() { (int)OrgKind.Company, (int)OrgKind.Dept },
        [(int)OrgKind.Company] = new() { (int)OrgKind.Dept },
        [(int)OrgKind.Dept]    = new() { (int)OrgKind.Dept, (int)OrgKind.Team },
        [(int)OrgKind.Team]    = new(),
    };

    /// <summary>合法根节点类别（F父ID=0）：集团/区域公司/网点公司（客户形态可变）。</summary>
    private static readonly HashSet<int> LegalRootKinds = new()
        { (int)OrgKind.Group, (int)OrgKind.Region, (int)OrgKind.Company };

    public static bool IsLegalRootKind(int kind) => LegalRootKinds.Contains(kind);

    public static bool IsLegalChild(int parentKind, int childKind)
        => LegalChildKinds.TryGetValue(parentKind, out var set) && set.Contains(childKind);

    /// <summary>
    /// 范围根解析（4 级）：最近网点公司祖先(含自身)→网点公司；否则最近"子树含网点公司的中心"→中心；
    /// 否则最近区域公司→区域公司；否则集团(根)→集团。<paramref name="chainSelfFirst"/> = [自身, 父, …, 根]。
    /// </summary>
    public static (long rootId, OrgScopeType rootType) ResolveScopeRoot(
        IReadOnlyList<SysOrganization> chainSelfFirst, IReadOnlySet<long> hasCompanyInSubtree)
    {
        var company = chainSelfFirst.FirstOrDefault(n => n.FKind == (int)OrgKind.Company);
        if (company != null) return (company.FID, OrgScopeType.Company);

        var center = chainSelfFirst.FirstOrDefault(
            n => n.FKind == (int)OrgKind.Center && hasCompanyInSubtree.Contains(n.FID));
        if (center != null) return (center.FID, OrgScopeType.Center);

        var region = chainSelfFirst.FirstOrDefault(n => n.FKind == (int)OrgKind.Region);
        if (region != null) return (region.FID, OrgScopeType.Region);

        var group = chainSelfFirst.FirstOrDefault(n => n.FKind == (int)OrgKind.Group);
        if (group != null) return (group.FID, OrgScopeType.Group);

        // 兜底：无任何已知类别祖先 → 以最顶祖先作集团级范围根。
        var top = chainSelfFirst[^1];
        return (top.FID, OrgScopeType.Group);
    }

    /// <summary>
    /// 全量重建物化字段 + 闭包表。幂等：可重复调用（切换/引导路径读取组织树不受 fail-closed 影响，组织树未挂租户硬墙）。
    /// 由 SystemSeeder(V7 首次物化) 与 OrganizationService(建/改/删/迁移节点后) 调用。
    /// </summary>
    public static void RebuildAll(STOTOPDbContext ctx)
    {
        var orgs = ctx.Set<SysOrganization>().IgnoreQueryFilters().AsTracking().ToList();
        if (orgs.Count == 0) return;

        var byId = orgs.ToDictionary(o => o.FID);
        var hasCompany = ComputeHasCompanyInSubtree(orgs, byId);

        foreach (var o in orgs)
        {
            var chain = AncestorChainSelfFirst(o, byId); // [自身, 父, …, 根]

            o.FTenantId = chain[^1].FID;                 // 租户根 = 最顶祖先 FID（单客户 = MDSTO）
            o.FParentKind = (o.FParentId > 0 && byId.TryGetValue(o.FParentId, out var parent))
                ? parent.FKind : (int?)null;
            o.FCompanyId = chain.FirstOrDefault(n => n.FKind == (int)OrgKind.Company)?.FID;

            var (rootId, rootType) = ResolveScopeRoot(chain, hasCompany);
            o.FScopeRootId = rootId;
            o.FScopeRootType = (int)rootType;

            // 最近可切换祖先(含自身) —— O(1) 切换列表用，语义同旧 FindSwitchableAncestor
            o.FSwitchRootId = chain.FirstOrDefault(n => n.FIsSwitchable)?.FID ?? 0;

            // 路径 /根/.../自身/
            var idsRootFirst = new List<long>();
            for (int i = chain.Count - 1; i >= 0; i--) idsRootFirst.Add(chain[i].FID);
            o.FPath = "/" + string.Join("/", idsRootFirst) + "/";
        }

        // 重建闭包（含自反 depth=0）
        var existing = ctx.Set<SysOrgClosure>().ToList();
        if (existing.Count > 0) ctx.Set<SysOrgClosure>().RemoveRange(existing);

        var rows = new List<SysOrgClosure>();
        foreach (var o in orgs)
        {
            var chain = AncestorChainSelfFirst(o, byId);
            for (int depth = 0; depth < chain.Count; depth++)
                rows.Add(new SysOrgClosure
                {
                    FAncestorId = chain[depth].FID,
                    FDescendantId = o.FID,
                    FDepth = depth,
                    FTenantId = o.FTenantId,
                });
        }
        ctx.Set<SysOrgClosure>().AddRange(rows);

        ctx.SaveChanges();
    }

    /// <summary>祖先链 [自身, 父, …, 根]，防环(visited)+深度上限。父缺失(孤儿)在最后可达祖先停。</summary>
    private static List<SysOrganization> AncestorChainSelfFirst(
        SysOrganization node, Dictionary<long, SysOrganization> byId)
    {
        var chain = new List<SysOrganization>();
        var visited = new HashSet<long>();
        var cur = node;
        while (cur != null && visited.Add(cur.FID) && chain.Count < 100)
        {
            chain.Add(cur);
            if (cur.FParentId <= 0) break;
            byId.TryGetValue(cur.FParentId, out cur);
        }
        return chain;
    }

    /// <summary>各节点子树(含自身)是否含网点公司——供中心级范围判定。做法：每个网点公司把其祖先链全标记。</summary>
    private static HashSet<long> ComputeHasCompanyInSubtree(
        List<SysOrganization> orgs, Dictionary<long, SysOrganization> byId)
    {
        var set = new HashSet<long>();
        foreach (var company in orgs.Where(x => x.FKind == (int)OrgKind.Company))
            foreach (var anc in AncestorChainSelfFirst(company, byId))
                set.Add(anc.FID);
        return set;
    }
}
