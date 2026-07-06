using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;
using System.Security.Claims;

namespace STOTOP.Module.System.Services;

public class PositionService : IPositionService
{
    private readonly STOTOPDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IChangeLogService _changeLogService;
    private readonly ITenantAdminScopeAccessor _tenantScope;

    public PositionService(STOTOPDbContext context, IHttpContextAccessor httpContextAccessor, IChangeLogService changeLogService, ITenantAdminScopeAccessor tenantScope)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _changeLogService = changeLogService;
        _tenantScope = tenantScope;
    }

    // ===== R5·stage4C 租户数据墙助手（SYS岗位 无租户列，经 关联组织/用户 定租户）=====

    /// <summary>指定组织是否在作用域内。</summary>
    private async Task<bool> AreOrgsInScopeAsync(IReadOnlyCollection<long> orgIds, TenantDataScope scope)
    {
        if (scope.IsPlatformAdmin || orgIds.Count == 0) return true;
        var tids = scope.TenantIds.ToList();
        if (tids.Count == 0) return false;
        var distinct = orgIds.Distinct().ToList();
        var inScope = await _context.Set<SysOrganization>()
            .CountAsync(o => distinct.Contains(o.FID) && tids.Contains(o.FTenantId));
        return inScope == distinct.Count;
    }

    /// <summary>指定用户是否均为作用域内租户的已接受成员。</summary>
    private async Task<bool> AreUsersInScopeAsync(IReadOnlyCollection<long> userIds, TenantDataScope scope)
    {
        if (scope.IsPlatformAdmin || userIds.Count == 0) return true;
        var tids = scope.TenantIds.ToList();
        if (tids.Count == 0) return false;
        var distinct = userIds.Distinct().ToList();
        var inScope = await _context.Set<SysTenantMember>()
            .Where(m => distinct.Contains(m.FUserId) && m.FInviteStatus == 2 && tids.Contains(m.FTenantId))
            .Select(m => m.FUserId).Distinct().CountAsync();
        return inScope == distinct.Count;
    }

    /// <summary>岗位是否在作用域内：无部门关联=全局目录(可见)，否则须至少一个部门属作用域租户。</summary>
    private async Task<bool> IsPositionInScopeAsync(long positionId, TenantDataScope scope)
    {
        if (scope.IsPlatformAdmin) return true;
        var tids = scope.TenantIds.ToList();
        if (tids.Count == 0) return false;
        var orgIds = await _context.Set<SysPositionDepartment>()
            .Where(pd => pd.FPositionId == positionId)
            .Select(pd => pd.FOrganizationId).ToListAsync();
        if (orgIds.Count == 0) return true; // 全局目录岗位
        return await _context.Set<SysOrganization>()
            .AnyAsync(o => orgIds.Contains(o.FID) && tids.Contains(o.FTenantId));
    }

    private (long? UserId, string? UserName) GetCurrentUser()
    {
        var claims = _httpContextAccessor.HttpContext?.User;
        var userIdStr = claims?.FindFirst("userId")?.Value ?? claims?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = claims?.FindFirst("userName")?.Value ?? claims?.FindFirst(ClaimTypes.Name)?.Value;
        long? userId = long.TryParse(userIdStr, out var id) ? id : null;
        return (userId, userName);
    }

    public async Task<(List<PositionDto> Items, int Total)> GetPagedListAsync(int pageIndex, int pageSize, string? keyword)
    {
        var query = _context.Set<SysPosition>().AsQueryable();

        // R5·stage4C：非平台 admin 只见「无部门关联(全局目录)」或「关联到本租户组织」的岗位。
        var scope = await _tenantScope.ResolveAsync();
        if (!scope.IsPlatformAdmin)
        {
            var tids = scope.TenantIds.ToList();
            query = query.Where(p => !p.PositionDepartments.Any()
                || p.PositionDepartments.Any(pd => tids.Contains(pd.Organization.FTenantId)));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p => p.FName.Contains(keyword) || p.FCode.Contains(keyword));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(p => p.FSort)
            .ThenByDescending(p => p.FCreateTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PositionDto
            {
                Id = p.FID,
                Uid = p.FUID,
                Name = p.FName,
                Code = p.FCode,
                Description = p.FDescription,
                Status = p.FStatus,
                DingTalkPositionId = p.FDingTalkPositionId,
                DingTalkBindStatus = p.FDingTalkBindStatus,
                Sort = p.FSort,
                CreateTime = p.FCreateTime,
                UpdateTime = p.FUpdateTime,
                OrganizationCount = p.PositionDepartments.Count,
                UserCount = p.UserPositions.Count
            })
            .ToListAsync();

        return (items, total);
    }

    public async Task<PositionDto?> GetByIdAsync(long id)
    {
        var position = await _context.Set<SysPosition>()
            .Include(p => p.PositionDepartments)
                .ThenInclude(pd => pd.Organization)
            .Include(p => p.UserPositions)
                .ThenInclude(up => up.User)
            .FirstOrDefaultAsync(p => p.FID == id);

        if (position == null) return null;

        // R5·stage4C：非平台 admin 不得读取仅属他租户组织的岗位（无部门关联=全局目录，可见）。
        var scope = await _tenantScope.ResolveAsync();
        if (!scope.IsPlatformAdmin)
        {
            var linkedTenants = position.PositionDepartments
                .Where(pd => pd.Organization != null)
                .Select(pd => pd.Organization!.FTenantId).ToList();
            if (linkedTenants.Count > 0 && !linkedTenants.Any(t => scope.TenantIds.Contains(t)))
                return null;
        }

        return new PositionDto
        {
            Id = position.FID,
            Uid = position.FUID,
            Name = position.FName,
            Code = position.FCode,
            Description = position.FDescription,
            Status = position.FStatus,
            DingTalkPositionId = position.FDingTalkPositionId,
            DingTalkBindStatus = position.FDingTalkBindStatus,
            Sort = position.FSort,
            CreateTime = position.FCreateTime,
            UpdateTime = position.FUpdateTime,
            OrganizationCount = position.PositionDepartments.Count,
            UserCount = position.UserPositions.Count,
            Organizations = position.PositionDepartments.Select(pd => new PositionOrganizationDto
            {
                OrganizationId = pd.FOrganizationId,
                OrganizationName = pd.Organization?.FName ?? ""
            }).ToList(),
            Users = position.UserPositions.Select(up => new PositionUserDto
            {
                UserId = up.FUserId,
                UserName = up.User?.FName ?? "",
                IsPrimary = up.FIsPrimary
            }).ToList()
        };
    }

    public async Task<PositionDto> CreateAsync(CreatePositionRequest request)
    {
        if (await _context.Set<SysPosition>().AnyAsync(p => p.FCode == request.Code))
            throw new InvalidOperationException("岗位编码已存在");

        // R5·stage4C：非平台 admin 不得把岗位关联到跨租户组织。
        var scope = await _tenantScope.ResolveAsync();
        if (!await AreOrgsInScopeAsync(request.OrganizationIds ?? Array.Empty<long>(), scope))
            throw new UnauthorizedAccessException("无权关联跨租户组织");

        var position = new SysPosition
        {
            FUID = Guid.NewGuid().ToString("N"),
            FName = request.Name,
            FCode = request.Code,
            FDescription = request.Description,
            FStatus = request.Status,
            FSort = request.Sort
        };

        await _context.Set<SysPosition>().AddAsync(position);
        await _context.SaveChangesAsync();

        // 关联组织
        if (request.OrganizationIds != null && request.OrganizationIds.Length > 0)
        {
            var orgLinks = request.OrganizationIds.Select(orgId => new SysPositionDepartment
            {
                FPositionId = position.FID,
                FOrganizationId = orgId
            });
            await _context.Set<SysPositionDepartment>().AddRangeAsync(orgLinks);
            await _context.SaveChangesAsync();
        }

        // 记录变更日志
        var (userId, userName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("岗位", position.FID, position.FName,
            "创建", $"创建岗位：{position.FName}（{position.FCode}）", userId, userName);

        return (await GetByIdAsync(position.FID))!;
    }

    public async Task UpdateAsync(long id, UpdatePositionRequest request)
    {
        var position = await _context.Set<SysPosition>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);

        if (position == null)
            throw new InvalidOperationException("岗位不存在");

        // R5·stage4C：非平台 admin 不得改写仅属他租户的岗位。
        var scope = await _tenantScope.ResolveAsync();
        if (!await IsPositionInScopeAsync(id, scope))
            throw new InvalidOperationException("岗位不存在");

        if (await _context.Set<SysPosition>().AnyAsync(p => p.FCode == request.Code && p.FID != id))
            throw new InvalidOperationException("岗位编码已存在");

        // 记录旧值用于对比
        var oldName = position.FName;
        var oldCode = position.FCode;
        var oldDescription = position.FDescription;
        var oldStatus = position.FStatus;
        var oldSort = position.FSort;

        position.FName = request.Name;
        position.FCode = request.Code;
        position.FDescription = request.Description;
        position.FStatus = request.Status;
        position.FSort = request.Sort;
        position.FUpdateTime = DateTime.Now;

        await _context.SaveChangesAsync();

        // 对比变更
        var changes = new Dictionary<string, object>();
        if (oldName != request.Name) changes["名称"] = new { 旧值 = oldName, 新值 = request.Name };
        if (oldCode != request.Code) changes["编码"] = new { 旧值 = oldCode, 新值 = request.Code };
        if (oldDescription != request.Description) changes["描述"] = new { 旧值 = oldDescription ?? "", 新值 = request.Description ?? "" };
        if (oldStatus != request.Status) changes["状态"] = new { 旧值 = oldStatus.ToString(), 新值 = request.Status.ToString() };
        if (oldSort != request.Sort) changes["排序"] = new { 旧值 = oldSort.ToString(), 新值 = request.Sort.ToString() };

        if (changes.Count > 0)
        {
            var changeJson = global::System.Text.Json.JsonSerializer.Serialize(changes,
                new global::System.Text.Json.JsonSerializerOptions { Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            var (userId, userName) = GetCurrentUser();
            await _changeLogService.LogChangeAsync("岗位", id, position.FName, "修改", changeJson, userId, userName);
        }
    }

    public async Task DeleteAsync(long id)
    {
        var position = await _context.Set<SysPosition>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);

        if (position == null)
            throw new InvalidOperationException("岗位不存在");

        // R5·stage4C：非平台 admin 不得删除仅属他租户的岗位。
        var scope = await _tenantScope.ResolveAsync();
        if (!await IsPositionInScopeAsync(id, scope))
            throw new InvalidOperationException("岗位不存在");

        // 删除关联关系
        var deptLinks = await _context.Set<SysPositionDepartment>().Where(pd => pd.FPositionId == id).ToListAsync();
        var userLinks = await _context.Set<SysUserPosition>().Where(up => up.FPositionId == id).ToListAsync();
        _context.Set<SysPositionDepartment>().RemoveRange(deptLinks);
        _context.Set<SysUserPosition>().RemoveRange(userLinks);

        _context.Set<SysPosition>().Remove(position);
        await _context.SaveChangesAsync();

        // 记录变更日志
        var (userId, userName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("岗位", id, position.FName, "删除",
            $"删除岗位：{position.FName}（{position.FCode}）", userId, userName);
    }

    public async Task AssignOrganizationsAsync(long positionId, long[] organizationIds)
    {
        var position = await _context.Set<SysPosition>().FindAsync(positionId);
        if (position == null)
            throw new InvalidOperationException("岗位不存在");

        // R5·stage4C：非平台 admin 不得给他租户岗位分配，或关联跨租户组织。
        var scope = await _tenantScope.ResolveAsync();
        if (!await IsPositionInScopeAsync(positionId, scope))
            throw new InvalidOperationException("岗位不存在");
        if (!await AreOrgsInScopeAsync(organizationIds, scope))
            throw new UnauthorizedAccessException("无权关联跨租户组织");

        // 移除现有关联
        var existingLinks = await _context.Set<SysPositionDepartment>()
            .Where(pd => pd.FPositionId == positionId).ToListAsync();
        _context.Set<SysPositionDepartment>().RemoveRange(existingLinks);

        // 添加新关联
        if (organizationIds.Length > 0)
        {
            var newLinks = organizationIds.Select(orgId => new SysPositionDepartment
            {
                FPositionId = positionId,
                FOrganizationId = orgId
            });
            await _context.Set<SysPositionDepartment>().AddRangeAsync(newLinks);
        }

        await _context.SaveChangesAsync();

        var (userId, userName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("岗位", positionId, position.FName, "分配组织",
            $"分配组织ID：[{string.Join(",", organizationIds)}]", userId, userName);
    }

    public async Task AssignUsersAsync(long positionId, long[] userIds)
    {
        var position = await _context.Set<SysPosition>().FindAsync(positionId);
        if (position == null)
            throw new InvalidOperationException("岗位不存在");

        // R5·stage4C：非平台 admin 不得给他租户岗位分配，或分配跨租户用户。
        var scope = await _tenantScope.ResolveAsync();
        if (!await IsPositionInScopeAsync(positionId, scope))
            throw new InvalidOperationException("岗位不存在");
        if (!await AreUsersInScopeAsync(userIds, scope))
            throw new UnauthorizedAccessException("无权分配跨租户用户");

        // 移除现有关联
        var existingLinks = await _context.Set<SysUserPosition>()
            .Where(up => up.FPositionId == positionId).ToListAsync();
        _context.Set<SysUserPosition>().RemoveRange(existingLinks);

        // 添加新关联
        if (userIds.Length > 0)
        {
            var newLinks = userIds.Select(uId => new SysUserPosition
            {
                FUserId = uId,
                FPositionId = positionId
            });
            await _context.Set<SysUserPosition>().AddRangeAsync(newLinks);
        }

        await _context.SaveChangesAsync();

        var (userId, userName) = GetCurrentUser();
        await _changeLogService.LogChangeAsync("岗位", positionId, position.FName, "分配人员",
            $"分配人员ID：[{string.Join(",", userIds)}]", userId, userName);
    }

    public async Task<List<PositionDto>> GetByOrganizationAsync(long orgId)
    {
        // R5·stage4C：非平台 admin 不得查询他租户组织的岗位。
        var scope = await _tenantScope.ResolveAsync();
        if (!await AreOrgsInScopeAsync(new[] { orgId }, scope))
            return new List<PositionDto>();

        return await _context.Set<SysPositionDepartment>()
            .Where(pd => pd.FOrganizationId == orgId)
            .Select(pd => pd.Position)
            .Select(p => new PositionDto
            {
                Id = p.FID,
                Uid = p.FUID,
                Name = p.FName,
                Code = p.FCode,
                Description = p.FDescription,
                Status = p.FStatus,
                DingTalkPositionId = p.FDingTalkPositionId,
                DingTalkBindStatus = p.FDingTalkBindStatus,
                Sort = p.FSort,
                CreateTime = p.FCreateTime,
                UpdateTime = p.FUpdateTime,
                OrganizationCount = p.PositionDepartments.Count,
                UserCount = p.UserPositions.Count
            })
            .ToListAsync();
    }

    public async Task<List<PositionDto>> GetByUserAsync(long userId)
    {
        // R5·stage4C：非平台 admin 不得查询他租户用户的岗位。
        var scope = await _tenantScope.ResolveAsync();
        if (!await AreUsersInScopeAsync(new[] { userId }, scope))
            return new List<PositionDto>();

        return await _context.Set<SysUserPosition>()
            .Where(up => up.FUserId == userId)
            .Select(up => up.Position)
            .Select(p => new PositionDto
            {
                Id = p.FID,
                Uid = p.FUID,
                Name = p.FName,
                Code = p.FCode,
                Description = p.FDescription,
                Status = p.FStatus,
                DingTalkPositionId = p.FDingTalkPositionId,
                DingTalkBindStatus = p.FDingTalkBindStatus,
                Sort = p.FSort,
                CreateTime = p.FCreateTime,
                UpdateTime = p.FUpdateTime,
                OrganizationCount = p.PositionDepartments.Count,
                UserCount = p.UserPositions.Count
            })
            .ToListAsync();
    }
}
