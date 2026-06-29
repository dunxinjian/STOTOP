using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    public OrgContextService(STOTOPDbContext context, IHttpContextAccessor httpContextAccessor, IChangeLogService changeLogService, ILogger<OrgContextService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _changeLogService = changeLogService;
        _logger = logger;
    }

    private (long? UserId, string? UserName) GetCurrentUser()
    {
        var claims = _httpContextAccessor.HttpContext?.User;
        var userIdStr = claims?.FindFirst("userId")?.Value ?? claims?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = claims?.FindFirst("userName")?.Value ?? claims?.FindFirst(ClaimTypes.Name)?.Value;
        long? userId = long.TryParse(userIdStr, out var id) ? id : null;
        return (userId, userName);
    }

    /// <summary>
    /// 在已加载的组织字典中，从 nodeId 沿 FParentId 向上遍历，
    /// 找到第一个 FIsSwitchable=true 的祖先节点。
    /// 如果节点本身就是可切换的，直接返回自身。
    /// </summary>
    private (long? Id, string? Name) FindSwitchableAncestor(long nodeId, Dictionary<long, SysOrganization> orgDict)
    {
        var visited = new HashSet<long>();
        var currentId = nodeId;

        while (orgDict.TryGetValue(currentId, out var current) && !visited.Contains(currentId))
        {
            visited.Add(currentId);
            if (current.FIsSwitchable)
                return (current.FID, current.FName);
            if (current.FParentId <= 0)
                break;
            currentId = current.FParentId;
        }

        return (null, null);
    }

    public async Task<List<UserOrganizationDto>> GetUserOrganizationsAsync(long userId)
    {
        // 0. 判断是否为 admin 用户
        var currentUser = await _context.Set<SysUser>().FirstOrDefaultAsync(u => u.FID == userId);
        var isAdmin = currentUser != null && string.Equals(currentUser.FAccount, "admin", StringComparison.OrdinalIgnoreCase);

        // 1. 一次性加载所有组织节点到内存，用于树遍历
        var allOrgs = await _context.Set<SysOrganization>()
            .Select(o => new { o.FID, o.FName, o.FParentId, o.FIsSwitchable, o.FTypeId })
            .ToListAsync();

        var orgDict = allOrgs.ToDictionary(
            o => o.FID,
            o => new SysOrganization { FID = o.FID, FName = o.FName, FParentId = o.FParentId, FIsSwitchable = o.FIsSwitchable, FTypeId = o.FTypeId }
        );

        // admin 用户：直接返回所有可切换组织
        if (isAdmin)
        {
            // 查询admin的主组织ID
            var adminPrimaryOrgId = await _context.Set<SysUserOrganization>()
                .Where(uo => uo.FUserId == userId && uo.FIsPrimaryOrg == 1)
                .Select(uo => uo.FOrgId)
                .FirstOrDefaultAsync();

            return allOrgs
                .Where(o => o.FIsSwitchable)
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

        // 2. 获取用户所有任职记录，Join 获取组织节点信息
        var userOrgs = await _context.Set<SysUserOrganization>()
            .Where(uo => uo.FUserId == userId)
            .Join(_context.Set<SysOrganization>(),
                uo => uo.FOrgId,
                org => org.FID,
                (uo, org) => new { uo, org })
            .GroupJoin(_context.Set<SysUser>(),
                x => x.uo.FDirectSuperiorId,
                sup => sup.FID,
                (x, sups) => new { x.uo, x.org, sups })
            .SelectMany(x => x.sups.DefaultIfEmpty(),
                (x, sup) => new
                {
                    x.uo,
                    x.org,
                    SuperiorName = sup != null ? sup.FName : null
                })
            .ToListAsync();

        if (!userOrgs.Any())
            return new List<UserOrganizationDto>();

        // 3. 对每条记录推导可切换祖先，过滤无可切换祖先的记录，按可切换组织去重
        var seen = new HashSet<long>();
        var result = new List<UserOrganizationDto>();

        foreach (var x in userOrgs)
        {
            var (switchableOrgId, switchableOrgName) = FindSwitchableAncestor(x.org.FID, orgDict);
            if (switchableOrgId == null || !seen.Add(switchableOrgId.Value))
                continue;

            result.Add(new UserOrganizationDto
            {
                Id = x.uo.FID,
                UserId = x.uo.FUserId,
                OrgId = switchableOrgId.Value,
                OrgName = switchableOrgName!,
                OrgType = orgDict.TryGetValue(switchableOrgId.Value, out var sOrg) ? sOrg.FTypeId.ToString() : x.org.FTypeId.ToString(),
                SwitchableOrgId = switchableOrgId,
                SwitchableOrgName = switchableOrgName,
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

        // 1. 验证用户确实属于该组织
        var userOrg = await _context.Set<SysUserOrganization>()
            .FirstOrDefaultAsync(uo => uo.FUserId == userId && uo.FOrgId == orgId);

        if (userOrg == null)
        {
            _logger.LogWarning("SwitchOrganization 失败: 用户 {UserId} 不属于组织 {OrgId}", userId, orgId);
            throw new InvalidOperationException("用户不属于该组织");
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

        _context.Set<SysUserOrganization>().Remove(userOrg);
        await _context.SaveChangesAsync();

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
