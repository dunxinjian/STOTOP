using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

public class RoleService : IRoleService
{
    private readonly STOTOPDbContext _context;
    private readonly ICodeRuleService _codeRuleService;
    private readonly ITenantAdminScopeAccessor _tenantScope;

    public RoleService(STOTOPDbContext context, ICodeRuleService codeRuleService, ITenantAdminScopeAccessor tenantScope)
    {
        _context = context;
        _codeRuleService = codeRuleService;
        _tenantScope = tenantScope;
    }

    // R5·stage4C：可改写=平台级 admin，或本租户私有角色；平台角色(含全局 role1)与他租户角色对非平台 admin 只读/不可改。
    private static bool IsRoleMutableInScope(SysRole role, TenantDataScope scope)
        => scope.IsPlatformAdmin || (role.FScope == SysRoleScope.Tenant && scope.TenantIds.Contains(role.FTenantId));

    public async Task<ApiResult<List<RoleDto>>> GetAllAsync()
    {
        // R5·stage4C：非平台 admin 只见平台共享角色 + 本租户私有角色。
        var scope = await _tenantScope.ResolveAsync();
        var roleQuery = _context.Set<SysRole>().AsQueryable();
        if (!scope.IsPlatformAdmin)
        {
            var tids = scope.TenantIds.ToList();
            roleQuery = roleQuery.Where(r => r.FScope == SysRoleScope.Platform || tids.Contains(r.FTenantId));
        }

        var roles = await roleQuery
            .OrderBy(r => r.FCreateTime)
            .ToListAsync();

        var dtos = roles.Select(r => new RoleDto
        {
            Id = r.FID,
            Name = r.FName,
            Code = r.FCode,
            Description = r.FDescription,
            Status = r.FStatus,
            CreateTime = r.FCreateTime
        }).ToList();

        return ApiResult<List<RoleDto>>.Success(dtos);
    }

    public async Task<ApiResult<RoleDto>> GetByIdAsync(long id)
    {
        var role = await _context.Set<SysRole>()
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.FID == id);

        if (role == null)
        {
            return ApiResult<RoleDto>.Fail("角色不存在");
        }

        // R5·stage4C：非平台 admin 不得读取他租户私有角色（不泄露存在性）。
        var scope = await _tenantScope.ResolveAsync();
        if (!scope.IsPlatformAdmin && role.FScope != SysRoleScope.Platform && !scope.TenantIds.Contains(role.FTenantId))
        {
            return ApiResult<RoleDto>.Fail("角色不存在");
        }

        var dto = new RoleDto
        {
            Id = role.FID,
            Name = role.FName,
            Code = role.FCode,
            Description = role.FDescription,
            Status = role.FStatus,
            CreateTime = role.FCreateTime,
            PermissionIds = role.RolePermissions.Select(rp => rp.FPermissionId).ToList()
        };

        return ApiResult<RoleDto>.Success(dto);
    }

    public async Task<ApiResult<RoleDto>> CreateAsync(CreateRoleRequest request)
    {
        // R5·stage4C：非平台 admin 只能建【本租户私有】非管理员角色（不得建平台级/管理员型角色）。
        var scope = await _tenantScope.ResolveAsync();
        if (!scope.IsPlatformAdmin && scope.TenantIds.Count == 0)
            return ApiResult<RoleDto>.Fail("无有效租户上下文，无法创建角色");

        // 自动生成角色编码（RL0001, RL0002, ...）
        var code = await _codeRuleService.GenerateNextCodeAsync("RL");

        var role = new SysRole
        {
            FName = request.Name,
            FCode = code,
            FDescription = request.Description,
            FStatus = request.Status
        };
        if (!scope.IsPlatformAdmin)
        {
            role.FScope = SysRoleScope.Tenant;
            role.FTenantId = scope.TenantIds[0];
            role.FIsAdmin = false;
        }

        await _context.Set<SysRole>().AddAsync(role);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(role.FID);
    }

    public async Task<ApiResult<RoleDto>> UpdateAsync(long id, UpdateRoleRequest request)
    {
        var role = await _context.Set<SysRole>().AsTracking().FirstOrDefaultAsync(r => r.FID == id);
        if (role == null)
        {
            return ApiResult<RoleDto>.Fail("角色不存在");
        }

        // R5·stage4C：非平台 admin 不得改写平台角色(含全局 role1)或他租户角色。
        var scope = await _tenantScope.ResolveAsync();
        if (!IsRoleMutableInScope(role, scope))
        {
            return ApiResult<RoleDto>.Fail("无权操作该角色");
        }

        role.FName = request.Name;
        role.FDescription = request.Description;
        role.FStatus = request.Status;
        role.FUpdateTime = DateTime.Now;

        await _context.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ApiResult<bool>> DeleteAsync(long id)
    {
        var role = await _context.Set<SysRole>().FindAsync(id);
        if (role == null)
        {
            return ApiResult<bool>.Fail("角色不存在");
        }

        // R5·stage4C：非平台 admin 不得删除平台角色(含全局 role1)或他租户角色。
        var scope = await _tenantScope.ResolveAsync();
        if (!IsRoleMutableInScope(role, scope))
        {
            return ApiResult<bool>.Fail("无权操作该角色");
        }

        _context.Set<SysRole>().Remove(role);
        await _context.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<bool>> AssignPermissionsAsync(long roleId, List<long> permissionIds)
    {
        var role = await _context.Set<SysRole>()
            .Include(r => r.RolePermissions)
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == roleId);

        if (role == null)
        {
            return ApiResult<bool>.Fail("角色不存在");
        }

        // R5·stage4C：非平台 admin 不得给平台角色(含全局 role1)或他租户角色授权（挡"给全局角色授权提权"）。
        var scope = await _tenantScope.ResolveAsync();
        if (!IsRoleMutableInScope(role, scope))
        {
            return ApiResult<bool>.Fail("无权操作该角色");
        }

        // 移除现有权限
        _context.Set<SysRolePermission>().RemoveRange(role.RolePermissions);

        // 添加新权限
        if (permissionIds.Any())
        {
            var rolePermissions = permissionIds.Select(pid => new SysRolePermission
            {
                FRoleId = roleId,
                FPermissionId = pid
            });
            await _context.Set<SysRolePermission>().AddRangeAsync(rolePermissions);
        }

        await _context.SaveChangesAsync();
        return ApiResult<bool>.Success(true, "权限分配成功");
    }
}
