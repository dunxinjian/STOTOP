using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;
using System.Security.Claims;

namespace STOTOP.Module.System.Services;

public class OrgContextService : IOrgContextService
{
    private readonly STOTOPDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IChangeLogService _changeLogService;
    private readonly ILogger<OrgContextService> _logger;
    private readonly IAdminAuthorizationService _adminAuth;
    private readonly IOrgContextAccessor _orgContextAccessor;

    public OrgContextService(STOTOPDbContext context, IHttpContextAccessor httpContextAccessor, IChangeLogService changeLogService, ILogger<OrgContextService> logger, IAdminAuthorizationService adminAuth, IOrgContextAccessor orgContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _changeLogService = changeLogService;
        _logger = logger;
        _adminAuth = adminAuth;
        _orgContextAccessor = orgContextAccessor;
    }

    private (long? UserId, string? UserName) GetCurrentUser()
    {
        var claims = _httpContextAccessor.HttpContext?.User;
        var userIdStr = claims?.FindFirst("userId")?.Value ?? claims?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = claims?.FindFirst("userName")?.Value ?? claims?.FindFirst(ClaimTypes.Name)?.Value;
        long? userId = long.TryParse(userIdStr, out var id) ? id : null;
        return (userId, userName);
    }

    public async Task<List<UserOrganizationDto>> GetUserOrganizationsAsync(long userId)
    {
        // 0. 判断是否为 admin 用户——口径统一：走中心 IAdminAuthorizationService（认 DB F角色ID=1），
        //    不再按 SYS用户.F账号 字面量 "admin" 判定。
        var isAdmin = await _adminAuth.IsAdminByUserIdAsync(_context, userId);

        // admin 用户：直接返回所有可切换组织
        if (isAdmin)
        {
            var adminPrimaryOrgId = await _context.Set<SysUserOrganization>()
                .Where(uo => uo.FUserId == userId && uo.FIsPrimaryOrg == 1)
                .Select(uo => uo.FOrgId)
                .FirstOrDefaultAsync();

            var switchable = await _context.Set<SysOrganization>()
                .Where(o => o.FIsSwitchable)
                .Select(o => new { o.FID, o.FName, o.FTypeId })
                .ToListAsync();

            return switchable
                .Select(o => new UserOrganizationDto
                {
                    Id = 0,
                    UserId = userId,
                    OrgId = o.FID,
                    OrgName = o.FName,
                    OrgType = o.FTypeId.ToString(),
                    SwitchableOrgId = o.FID,
                    SwitchableOrgName = o.FName,
                    IsPrimaryOrg = o.FID == adminPrimaryOrgId ? 1 : 0,
                    Status = 1
                })
                .ToList();
        }

        // M3：非 admin —— 用户当前任职 →(经 2A 物化的 F可切换根ID 连回可切换节点)得切换目标。
        // O(任职数) 单查询,不再全表载入组织 + 运行时上溯(退役 FindSwitchableAncestor);语义与旧一致。
        // F可切换根ID=0(无可切换祖先)的任职经内连接自然过滤掉(等价旧 switchableOrgId==null → skip)。
        var rows = await _context.Set<SysUserOrganization>()
            .Where(uo => uo.FUserId == userId && uo.F是否当前)
            .Join(_context.Set<SysOrganization>(),
                uo => uo.FOrgId, org => org.FID, (uo, org) => new { uo, org })
            .Join(_context.Set<SysOrganization>(),
                x => x.org.FSwitchRootId, sw => sw.FID, (x, sw) => new { x.uo, sw })
            .GroupJoin(_context.Set<SysUser>(),
                x => x.uo.FDirectSuperiorId, sup => sup.FID, (x, sups) => new { x.uo, x.sw, sups })
            .SelectMany(x => x.sups.DefaultIfEmpty(),
                (x, sup) => new { x.uo, x.sw, SuperiorName = sup != null ? sup.FName : null })
            .ToListAsync();

        var seen = new HashSet<long>();
        var result = new List<UserOrganizationDto>();
        foreach (var x in rows)
        {
            if (!seen.Add(x.sw.FID)) continue; // 按可切换根去重
            result.Add(new UserOrganizationDto
            {
                Id = x.uo.FID,
                UserId = x.uo.FUserId,
                OrgId = x.sw.FID,
                OrgName = x.sw.FName,
                OrgType = x.sw.FTypeId.ToString(),
                SwitchableOrgId = x.sw.FID,
                SwitchableOrgName = x.sw.FName,
                DirectSuperiorId = x.uo.FDirectSuperiorId,
                DirectSuperiorName = x.SuperiorName,
                IsPrimaryOrg = x.uo.FIsPrimaryOrg,
                Position = x.uo.FPosition,
                JobNumber = x.uo.FJobNumber,
                EntryDate = x.uo.FEntryDate,
                Status = x.uo.FStatus
            });
        }

        return result;
    }

    public async Task<SwitchOrganizationResponse> SwitchOrganizationAsync(long userId, long orgId)
    {
        _logger.LogInformation("SwitchOrganization 开始: userId={UserId}, orgId={OrgId}", userId, orgId);

        // 1. 验证用户确实属于该组织。M3 口径收敛：admin 与 GetUserOrganizations/中间件 一致——
        //    可切换到任一 FIsSwitchable 组织(不要求成员行),普通用户须有该组织(或其可切换子)任职。
        var isAdmin = await _adminAuth.IsAdminByUserIdAsync(_context, userId);
        if (!isAdmin)
        {
            var belongs = await _context.Set<SysUserOrganization>()
                .AnyAsync(uo => uo.FUserId == userId && uo.FOrgId == orgId);
            // 也接受"该 orgId 是用户某任职的可切换根"(与切换列表口径一致)
            if (!belongs)
                belongs = (await GetUserOrganizationsAsync(userId)).Any(u => u.OrgId == orgId);
            if (!belongs)
            {
                _logger.LogWarning("SwitchOrganization 失败: 用户 {UserId} 不属于组织 {OrgId}", userId, orgId);
                throw new InvalidOperationException("用户不属于该组织");
            }
        }

        var org = await _context.Set<SysOrganization>().FindAsync(orgId);
        if (org == null)
        {
            _logger.LogWarning("SwitchOrganization 失败: 组织 {OrgId} 不存在", orgId);
            throw new InvalidOperationException("组织不存在");
        }

        _logger.LogInformation("SwitchOrganization 组织详情: orgId={OrgId}, name={Name}, FIsSwitchable={IsSwitchable}, FStatus={Status}",
            orgId, org.FName, org.FIsSwitchable, org.FStatus);

        if (!org.FIsSwitchable)
        {
            _logger.LogWarning("SwitchOrganization 失败: 组织 {OrgId}({Name}) 未列入切换列表, FIsSwitchable={IsSwitchable}",
                orgId, org.FName, org.FIsSwitchable);
            throw new InvalidOperationException("该组织未列入切换列表");
        }

        // 2. 查询该用户在该组织下的角色（FOrgId=orgId OR FOrgId IS NULL 表示全局角色）
        var roleIds = await _context.Set<SysUserRole>()
            .Where(ur => ur.FUserId == userId && (ur.FOrgId == orgId || ur.FOrgId == null))
            .Select(ur => ur.FRoleId)
            .Distinct()
            .ToListAsync();

        var roles = await _context.Set<SysRole>()
            .Where(r => roleIds.Contains(r.FID))
            .Select(r => r.FCode)
            .ToListAsync();

        // 3. 根据角色查询权限
        var permissionIds = await _context.Set<SysRolePermission>()
            .Where(rp => roleIds.Contains(rp.FRoleId))
            .Select(rp => rp.FPermissionId)
            .Distinct()
            .ToListAsync();

        var permissions = await _context.Set<SysPermission>()
            .Where(p => permissionIds.Contains(p.FID))
            .ToListAsync();

        var permissionCodes = permissions
            .Select(p => p.FCode)
            .Distinct()
            .ToList();

        // 4. 构建菜单树（参考 AuthService）
        var menuPermissions = permissions
            .Where(p => p.FType == "模块" || p.FType == "菜单")
            .OrderBy(p => p.FParentId)
            .ThenBy(p => p.FSort)
            .ToList();

        var menuDtos = menuPermissions.Select(p => new MenuDto
        {
            Id = p.FID,
            Name = p.FName,
            Code = p.FCode,
            Icon = p.FIcon,
            Route = p.FRoute,
            ComponentPath = p.FComponentPath,
            Type = p.FType == "模块" ? "module" : (p.FType == "按钮" ? "button" : "menu"),
            Sort = p.FSort,
            ParentId = p.FParentId,
            IsVisible = p.FIsVisible
        }).ToList();

        var menus = BuildMenuTree(menuDtos);

        // 5. 返回 SwitchOrganizationResponse
        return new SwitchOrganizationResponse
        {
            OrgId = orgId,
            OrgName = org.FName,
            OrgType = org.FTypeId.ToString(),
            Roles = roles,
            Permissions = permissionCodes,
            Menus = menus
        };
    }

    public async Task<UserOrganizationDto?> GetCurrentContextAsync(long userId, long orgId)
    {
        var userOrgs = await GetUserOrganizationsAsync(userId);
        return userOrgs.FirstOrDefault(uo => uo.OrgId == orgId);
    }

    public async Task AddUserToOrganizationAsync(AddUserToOrganizationRequest request)
    {
        // 唯一性校验
        var exists = await _context.Set<SysUserOrganization>()
            .AnyAsync(uo => uo.FUserId == request.UserId && uo.FOrgId == request.OrgId);

        if (exists)
            throw new InvalidOperationException("用户已在该组织中");

        var userOrg = new SysUserOrganization
        {
            FUserId = request.UserId,
            FOrgId = request.OrgId,
            FDirectSuperiorId = request.DirectSuperiorId,
            FIsPrimaryOrg = request.IsPrimaryOrg,
            FPosition = request.Position,
            FJobNumber = request.JobNumber,
            FEntryDate = request.EntryDate,
            FStatus = 1
        };

        await _context.Set<SysUserOrganization>().AddAsync(userOrg);
        await _context.SaveChangesAsync();

        // M3 增量双写：同步 SYS租户成员 + SYS任职（best-effort，绝不影响主写入）
        await SyncNewTablesBestEffortAsync(request.UserId, request.OrgId, "add");

        // 记录变更日志
        var user = await _context.Set<SysUser>().FindAsync(request.UserId);
        var org = await _context.Set<SysOrganization>().FindAsync(request.OrgId);
        var (operatorId, operatorName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("用户组织", userOrg.FID,
            $"{user?.FName ?? ""}-{org?.FName ?? ""}",
            "添加", $"用户[{user?.FName}]加入组织[{org?.FName}]", operatorId, operatorName);
    }

    public async Task UpdateUserOrganizationAsync(long id, UpdateUserOrganizationRequest request)
    {
        var userOrg = await _context.Set<SysUserOrganization>()
            .AsTracking()
            .FirstOrDefaultAsync(uo => uo.FID == id);

        if (userOrg == null)
            throw new InvalidOperationException("用户组织记录不存在");

        userOrg.FDirectSuperiorId = request.DirectSuperiorId;
        if (request.IsPrimaryOrg.HasValue) userOrg.FIsPrimaryOrg = request.IsPrimaryOrg.Value;
        userOrg.FPosition = request.Position;
        userOrg.FJobNumber = request.JobNumber;
        userOrg.FEntryDate = request.EntryDate;
        if (request.Status.HasValue) userOrg.FStatus = request.Status.Value;
        userOrg.FUpdateTime = DateTime.Now;

        await _context.SaveChangesAsync();

        // M3 增量双写
        await SyncNewTablesBestEffortAsync(userOrg.FUserId, userOrg.FOrgId, "update");

        var (operatorId, operatorName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("用户组织", id, $"用户组织记录#{id}",
            "修改", "更新用户组织任职信息", operatorId, operatorName);
    }

    public async Task RemoveUserFromOrganizationAsync(long id)
    {
        var userOrg = await _context.Set<SysUserOrganization>()
            .AsTracking()
            .FirstOrDefaultAsync(uo => uo.FID == id);

        if (userOrg == null)
            throw new InvalidOperationException("用户组织记录不存在");

        var user = await _context.Set<SysUser>().FindAsync(userOrg.FUserId);
        var org = await _context.Set<SysOrganization>().FindAsync(userOrg.FOrgId);
        var removedUserId = userOrg.FUserId;
        var removedOrgId = userOrg.FOrgId;

        _context.Set<SysUserOrganization>().Remove(userOrg);
        await _context.SaveChangesAsync();

        // M3 增量双写：移除对应任职（best-effort）
        await SyncNewTablesBestEffortAsync(removedUserId, removedOrgId, "remove");

        var (operatorId, operatorName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("用户组织", userOrg.FID,
            $"{user?.FName ?? ""}-{org?.FName ?? ""}",
            "删除", $"用户[{user?.FName}]移出组织[{org?.FName}]", operatorId, operatorName);
    }

    public async Task<List<string>> GetOrgScopedRolesAsync(long userId, long orgId)
    {
        var roleIds = await _context.Set<SysUserRole>()
            .Where(ur => ur.FUserId == userId && (ur.FOrgId == orgId || ur.FOrgId == null))
            .Select(ur => ur.FRoleId)
            .Distinct()
            .ToListAsync();

        return await _context.Set<SysRole>()
            .Where(r => roleIds.Contains(r.FID))
            .Select(r => r.FCode)
            .ToListAsync();
    }

    /// <summary>
    /// M3 增量双写：把 SYS用户组织 的建/改/删同步到新表 SYS租户成员 + SYS任职（喂 2D R8 的 FScopeEligible）。
    /// **best-effort**：任何异常仅告警、绝不影响主 SYS用户组织 写入（如无租户上下文时 SYS任职 撞 fail-closed 写硬墙）。
    /// 旧 10 个读消费者仍读 SYS用户组织(增量安全)；新表由本双写 + 回填(SystemSeeder V10) 保持一致,退役旧表留收尾。
    /// </summary>
    private async Task SyncNewTablesBestEffortAsync(long userId, long orgId, string op)
    {
        try
        {
            // 用请求租户上下文(与写硬墙同源)——无上下文则跳过双写，避免撞 fail-closed 写硬墙(见 rule-review)。
            var tenantId = _orgContextAccessor?.CurrentTenantId;
            if (tenantId == null) return;

            var member = await _context.Set<SysTenantMember>()
                .FirstOrDefaultAsync(m => m.FUserId == userId && m.FTenantId == tenantId.Value);

            var appt = member == null ? null : await _context.Set<SysAppointment>()
                .FirstOrDefaultAsync(a => a.FMemberId == member.FID && a.FOrgId == orgId && a.FIsCurrent);

            if (op == "remove")
            {
                if (appt != null)
                {
                    _context.Set<SysAppointment>().Remove(appt);
                    await _context.SaveChangesAsync();
                }
                return;
            }

            // add/update：确保成员存在
            if (member == null)
            {
                member = new SysTenantMember
                {
                    FUserId = userId,
                    FTenantId = tenantId.Value,
                    FIsPrimary = true,        // 单客户：唯一租户即主租户
                    FInviteStatus = 2,        // 已接受
                    FJoinedAt = DateTime.Now,
                    FStatus = 1
                };
                await _context.Set<SysTenantMember>().AddAsync(member);
                await _context.SaveChangesAsync();
            }

            var uo = await _context.Set<SysUserOrganization>()
                .FirstOrDefaultAsync(x => x.FUserId == userId && x.FOrgId == orgId && x.F是否当前);
            if (uo == null) return;

            if (appt == null)
            {
                appt = new SysAppointment { FTenantId = tenantId.Value, FMemberId = member.FID, FOrgId = orgId };
                await _context.Set<SysAppointment>().AddAsync(appt);
            }
            appt.FDirectSuperiorId = uo.FDirectSuperiorId;
            appt.FIsPrimary = uo.FIsPrimaryOrg == 1;
            appt.FScopeEligible = uo.FIsPrimaryOrg == 1; // 主任职默认可放大范围；非主(挂名/借调)不放大
            appt.FPosition = uo.FPosition;
            appt.FJobNumber = uo.FJobNumber;
            appt.FEntryDate = uo.FEntryDate;
            appt.FIsCurrent = uo.F是否当前;
            appt.FStatus = uo.FStatus;
            appt.FUpdateTime = DateTime.Now;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            DetachPendingMembershipEntities(); // 剔除失败的 Added/Modified，防污染共享 ChangeTracker 反噬主写入/LogChange
            _logger.LogWarning(ex, "M3 双写 SYS租户成员/SYS任职 失败(best-effort,不影响主写入): userId={UserId} orgId={OrgId} op={Op}", userId, orgId, op);
        }
    }

    /// <summary>best-effort 双写失败时，把仍挂在共享 DbContext 上的 SYS租户成员/SYS任职 变更剔除(Detach)，
    /// 防其在后续 SaveChanges(LogChange/下一用户) 被 flush 再次撞写硬墙、反噬主流程。</summary>
    private void DetachPendingMembershipEntities()
    {
        foreach (var e in _context.ChangeTracker.Entries<SysAppointment>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
            e.State = EntityState.Detached;
        foreach (var e in _context.ChangeTracker.Entries<SysTenantMember>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
            e.State = EntityState.Detached;
    }

    /// <summary>
    /// M3：按当前 SYS用户组织(F是否当前) 全量调和某用户的 SYS租户成员 + SYS任职。best-effort(异常仅告警)。
    /// 供 DingTalk 批量部门同步(RemoveRange+重建 SYS用户组织)后调用,保新表随之更新;idempotent。
    /// </summary>
    public async Task ReconcileUserMembershipBestEffortAsync(long userId)
    {
        try
        {
            // 用请求租户上下文(与写硬墙同源)——无上下文则跳过双写，避免撞 fail-closed 写硬墙。
            var tenantId = _orgContextAccessor?.CurrentTenantId;
            if (tenantId == null) return;

            var member = await _context.Set<SysTenantMember>()
                .FirstOrDefaultAsync(m => m.FUserId == userId && m.FTenantId == tenantId.Value);
            if (member == null)
            {
                member = new SysTenantMember
                {
                    FUserId = userId,
                    FTenantId = tenantId.Value,
                    FIsPrimary = true,
                    FInviteStatus = 2,
                    FJoinedAt = DateTime.Now,
                    FStatus = 1
                };
                await _context.Set<SysTenantMember>().AddAsync(member);
                await _context.SaveChangesAsync();
            }

            var currentUos = await _context.Set<SysUserOrganization>()
                .Where(uo => uo.FUserId == userId && uo.F是否当前)
                .ToListAsync();
            var currentOrgIds = currentUos.Select(u => u.FOrgId).ToHashSet();

            var appts = await _context.Set<SysAppointment>()
                .Where(a => a.FMemberId == member.FID)
                .ToListAsync();

            // 移除已不在当前任职的
            var stale = appts.Where(a => !currentOrgIds.Contains(a.FOrgId)).ToList();
            if (stale.Count > 0) _context.Set<SysAppointment>().RemoveRange(stale);

            // upsert 当前任职
            foreach (var uo in currentUos)
            {
                var appt = appts.FirstOrDefault(a => a.FOrgId == uo.FOrgId);
                if (appt == null)
                {
                    appt = new SysAppointment { FTenantId = tenantId.Value, FMemberId = member.FID, FOrgId = uo.FOrgId };
                    await _context.Set<SysAppointment>().AddAsync(appt);
                }
                appt.FDirectSuperiorId = uo.FDirectSuperiorId;
                appt.FIsPrimary = uo.FIsPrimaryOrg == 1;
                appt.FScopeEligible = uo.FIsPrimaryOrg == 1;
                appt.FPosition = uo.FPosition;
                appt.FJobNumber = uo.FJobNumber;
                appt.FEntryDate = uo.FEntryDate;
                appt.FIsCurrent = true;
                appt.FStatus = uo.FStatus;
                appt.FUpdateTime = DateTime.Now;
            }
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            DetachPendingMembershipEntities(); // 剔除失败的 Added/Modified，防污染共享 ChangeTracker 反噬后续 SaveChanges
            _logger.LogWarning(ex, "M3 调和用户 {UserId} 成员/任职失败(best-effort,不影响主同步)", userId);
        }
    }

    /// <summary>M3：O(成员数) 查用户可切换的租户列表（SYS租户成员，已接受）。阶段4 前端多租户切换用；单客户下通常 1 个。</summary>
    public async Task<List<TenantMembershipDto>> GetMyTenantsAsync(long userId)
    {
        return await _context.Set<SysTenantMember>()
            .Where(m => m.FUserId == userId && m.FInviteStatus == 2 && m.FStatus == 1)
            .Join(_context.Set<SysOrganization>(),
                m => m.FTenantId, o => o.FID, (m, o) => new TenantMembershipDto
                {
                    TenantId = m.FTenantId,
                    TenantName = o.FName,
                    IsPrimary = m.FIsPrimary
                })
            .ToListAsync();
    }

    private static List<MenuDto> BuildMenuTree(List<MenuDto> menus)
    {
        var menuLookup = menus.ToLookup(m => m.ParentId);
        var rootMenus = new List<MenuDto>();

        foreach (var menu in menus.Where(m => m.ParentId == 0))
        {
            rootMenus.Add(BuildMenuNode(menu, menuLookup));
        }

        return rootMenus.OrderBy(m => m.Sort).ToList();
    }

    private static MenuDto BuildMenuNode(MenuDto menu, ILookup<long, MenuDto> menuLookup)
    {
        var children = menuLookup[menu.Id]
            .OrderBy(m => m.Sort)
            .Select(m => BuildMenuNode(m, menuLookup))
            .ToList();

        menu.Children = children;
        return menu;
    }
}
