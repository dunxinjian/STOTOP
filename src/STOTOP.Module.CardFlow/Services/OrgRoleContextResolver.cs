using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.CardFlow.Services;

public sealed record OrgRoleContext(List<string> OrgChain, List<string> RoleCodes, List<string> RoleNames);

public static class OrgRoleContextResolver
{
    /// <summary>
    /// 从 startOrgId 沿 FParentId 上溯，含本组织，每级放 FID.ToString()。
    /// visited 防环；startOrgId 不在 orgs（停用/缺失）→ 空链。纯函数。
    /// </summary>
    public static List<string> BuildOrgChain(
        IReadOnlyCollection<(long Id, long ParentId)> orgs,
        long startOrgId)
    {
        var parentOf = new Dictionary<long, long>();
        foreach (var (id, parentId) in orgs)
            parentOf[id] = parentId;

        var chain = new List<string>();
        var visited = new HashSet<long>();
        var node = startOrgId;
        while (parentOf.TryGetValue(node, out var parent) && visited.Add(node))
        {
            chain.Add(node.ToString());
            node = parent;
        }
        return chain;
    }

    /// <summary>
    /// 加载 active 组织上溯 startOrgId 的 OrgChain；查 userId 的角色编码/名称。
    /// userId 空/≤0 → 角色空。运行时与预演共用。
    /// </summary>
    public static async Task<OrgRoleContext> ResolveAsync(
        STOTOPDbContext db, long orgId, long? userId, CancellationToken cancellationToken = default)
    {
        var orgRows = await db.Set<SysOrganization>()
            .Where(o => o.FStatus == 1)
            .Select(o => new { o.FID, o.FParentId })
            .ToListAsync(cancellationToken);
        var orgChain = BuildOrgChain(
            orgRows.Select(o => (o.FID, o.FParentId)).ToList(), orgId);

        var roleCodes = new List<string>();
        var roleNames = new List<string>();
        if (userId is long uid && uid > 0)
        {
            var roles = await db.Set<SysUserRole>()
                .Where(ur => ur.FUserId == uid)
                .Join(db.Set<SysRole>().Where(r => r.FStatus == 1), ur => ur.FRoleId, r => r.FID, (ur, r) => new { r.FCode, r.FName })
                .ToListAsync(cancellationToken);
            roleCodes = roles.Select(x => x.FCode).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            roleNames = roles.Select(x => x.FName).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        }

        return new OrgRoleContext(orgChain, roleCodes, roleNames);
    }
}
