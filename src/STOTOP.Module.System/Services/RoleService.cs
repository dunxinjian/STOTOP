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

    public RoleService(STOTOPDbContext context, ICodeRuleService codeRuleService)
    {
        _context = context;
        _codeRuleService = codeRuleService;
    }

    public async Task<ApiResult<List<RoleDto>>> GetAllAsync()
    {
        var roles = await _context.Set<SysRole>()
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
        // 自动生成角色编码（RL0001, RL0002, ...）
        var code = await _codeRuleService.GenerateNextCodeAsync("RL");

        var role = new SysRole
        {
            FName = request.Name,
            FCode = code,
            FDescription = request.Description,
            FStatus = request.Status
        };

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
