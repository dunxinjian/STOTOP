using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

public class OrgTypeService : IOrgTypeService
{
    private readonly STOTOPDbContext _context;

    public OrgTypeService(STOTOPDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResult<List<OrgTypeDto>>> GetAllAsync()
    {
        var list = await _context.Set<SysOrgType>()
            .Where(t => t.FIsEnabled)
            .OrderBy(t => t.FLevel)
            .ThenBy(t => t.FSortOrder)
            .Select(t => MapToDto(t))
            .ToListAsync();
        return ApiResult<List<OrgTypeDto>>.Success(list);
    }

    public async Task<ApiResult<List<OrgTypeDto>>> GetForAccountSetAsync()
    {
        var list = await _context.Set<SysOrgType>()
            .Where(t => t.FIsEnabled && t.FCanBindAccountSet)
            .OrderBy(t => t.FLevel)
            .ThenBy(t => t.FSortOrder)
            .Select(t => MapToDto(t))
            .ToListAsync();
        return ApiResult<List<OrgTypeDto>>.Success(list);
    }

    public async Task<ApiResult<OrgTypeDto?>> GetByIdAsync(long id)
    {
        var entity = await _context.Set<SysOrgType>().FindAsync(id);
        if (entity == null)
            return ApiResult<OrgTypeDto?>.Fail("组织类型不存在");
        return ApiResult<OrgTypeDto?>.Success(MapToDto(entity));
    }

    public async Task<ApiResult<bool>> UpdateAsync(long id, OrgTypeUpdateDto dto)
    {
        var entity = await _context.Set<SysOrgType>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id);
        if (entity == null)
            return ApiResult<bool>.Fail("组织类型不存在");

        if (dto.Icon != null) entity.FIcon = dto.Icon;
        if (dto.Sort.HasValue) entity.FSortOrder = dto.Sort.Value;
        if (dto.Description != null) entity.FDescription = dto.Description;
        if (dto.IsEnabled.HasValue) entity.FIsEnabled = dto.IsEnabled.Value;

        await _context.SaveChangesAsync();
        return ApiResult<bool>.Success(true);
    }

    private static OrgTypeDto MapToDto(SysOrgType t) => new()
    {
        Id = t.FID,
        Code = t.FCode,
        Name = t.FName,
        Level = t.FLevel,
        CanBindAccountSet = t.FCanBindAccountSet,
        CanSwitch = t.FCanSwitch,
        Icon = t.FIcon,
        Sort = t.FSortOrder,
        Description = t.FDescription,
        IsEnabled = t.FIsEnabled
    };
}
