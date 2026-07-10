using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>发起范围/代提交范围成员判定：给定用户归属(角色/组织/岗位) union 比对 scope。空 scope=不限制。</summary>
public sealed class InitiatorScopeResolver : IInitiatorScopeResolver
{
    private readonly STOTOPDbContext _dbContext;
    public InitiatorScopeResolver(STOTOPDbContext dbContext) => _dbContext = dbContext;

    public async Task<UserMemberships> GetUserMembershipsAsync(long userId, CancellationToken ct = default)
    {
        var roleIds = await _dbContext.Set<SysUserRole>()
            .Where(ur => ur.FUserId == userId).Select(ur => ur.FRoleId).ToListAsync(ct);
        var orgIds = await _dbContext.Set<SysUserOrganization>()
            .Where(uo => uo.FUserId == userId && uo.FStatus == 1).Select(uo => uo.FOrgId).ToListAsync(ct);
        var positionIds = await _dbContext.Set<SysUserPosition>()
            .Where(up => up.FUserId == userId).Select(up => up.FPositionId).ToListAsync(ct);
        return new UserMemberships(roleIds.ToHashSet(), orgIds.ToHashSet(), positionIds.ToHashSet());
    }

    public bool IsInScope(UserMemberships memberships, long userId, InitiatorScope? scope)
    {
        if (scope == null || scope.IsEmpty) return true;                    // 不限制
        if (scope.Users.Contains(userId)) return true;
        if (scope.Roles.Any(memberships.RoleIds.Contains)) return true;
        if (scope.Orgs.Any(memberships.OrgIds.Contains)) return true;
        if (scope.Positions.Any(memberships.PositionIds.Contains)) return true;
        return false;
    }
}
