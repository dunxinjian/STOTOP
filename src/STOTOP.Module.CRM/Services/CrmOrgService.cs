using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services.Interfaces;

namespace STOTOP.Module.CRM.Services;

public class CrmOrgService : ICrmOrgService
{
    private readonly IRepository<CrmRoleMapping> _roleMappingRepository;

    public CrmOrgService(IRepository<CrmRoleMapping> roleMappingRepository)
    {
        _roleMappingRepository = roleMappingRepository;
    }

    public async Task<PagedResult<CrmRoleMappingListItemDto>> GetRoleMappingsAsync(RoleMappingQueryRequest request)
    {
        var query = _roleMappingRepository.Query();

        if (request.OrgId.HasValue)
        {
            query = query.Where(r => r.FOrgId == request.OrgId.Value);
        }

        if (request.Role.HasValue)
        {
            query = query.Where(r => r.FRole == request.Role.Value);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<CrmRoleMappingListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<CrmRoleMappingDto?> GetRoleMappingByIdAsync(long id)
    {
        var entity = await _roleMappingRepository.Query()
            .FirstOrDefaultAsync(r => r.FID == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<CrmRoleMappingDto> CreateRoleMappingAsync(CreateRoleMappingRequest request)
    {
        // 检查是否已存在相同的角色映射
        var exists = await _roleMappingRepository.Query()
            .AnyAsync(r => r.FOrgId == request.OrgId && r.FEmployeeId == request.EmployeeId && r.FRole == request.Role);
        if (exists)
        {
            throw new InvalidOperationException("该员工在此组织下已存在相同角色映射");
        }

        var entity = new CrmRoleMapping
        {
            FOrgId = request.OrgId,
            FEmployeeId = request.EmployeeId,
            FRole = request.Role,
            FCreatedTime = DateTime.Now
        };

        await _roleMappingRepository.AddAsync(entity);
        return MapToDto(entity);
    }

    public async Task<CrmRoleMappingDto?> UpdateRoleMappingAsync(long id, UpdateRoleMappingRequest request)
    {
        var entity = await _roleMappingRepository.Query()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id);

        if (entity == null) return null;

        entity.FRole = request.Role;
        entity.FUpdaterName = null;
        entity.FUpdatedTime = DateTime.Now;

        await _roleMappingRepository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteRoleMappingAsync(long id)
    {
        var entity = await _roleMappingRepository.GetByIdAsync(id);
        if (entity == null) return false;

        await _roleMappingRepository.DeleteAsync(id);
        return true;
    }

    public async Task<List<CrmRoleMappingListItemDto>> GetBdListAsync(long orgId)
    {
        var items = await _roleMappingRepository.Query()
            .Where(r => r.FOrgId == orgId && r.FRole == 1)
            .OrderByDescending(r => r.FCreatedTime)
            .ToListAsync();

        return items.Select(MapToListItemDto).ToList();
    }

    public async Task<List<CrmRoleMappingListItemDto>> GetMaintenanceListAsync(long orgId)
    {
        var items = await _roleMappingRepository.Query()
            .Where(r => r.FOrgId == orgId && r.FRole == 2)
            .OrderByDescending(r => r.FCreatedTime)
            .ToListAsync();

        return items.Select(MapToListItemDto).ToList();
    }

    #region Mapping

    private static CrmRoleMappingDto MapToDto(CrmRoleMapping entity)
    {
        return new CrmRoleMappingDto
        {
            Id = entity.FID,
            OrgId = entity.FOrgId,
            EmployeeId = entity.FEmployeeId,
            Role = entity.FRole,
            CreatorName = entity.FCreatorName,
            CreatedTime = entity.FCreatedTime
        };
    }

    private static CrmRoleMappingListItemDto MapToListItemDto(CrmRoleMapping entity)
    {
        return new CrmRoleMappingListItemDto
        {
            Id = entity.FID,
            OrgId = entity.FOrgId,
            EmployeeId = entity.FEmployeeId,
            Role = entity.FRole,
            CreatedTime = entity.FCreatedTime
        };
    }

    #endregion
}
