using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private readonly STOTOPDbContext _context;

    public SystemSettingsService(STOTOPDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResult<List<SysConfigDto>>> GetAllAsync()
    {
        var items = await _context.Set<SysConfig>()
            .OrderBy(c => c.FKey)
            .ToListAsync();

        var dtos = items.Select(MapToDto).ToList();
        return ApiResult<List<SysConfigDto>>.Success(dtos);
    }

    public async Task<ApiResult<SysConfigDto?>> GetByKeyAsync(string key)
    {
        var config = await _context.Set<SysConfig>()
            .FirstOrDefaultAsync(c => c.FKey == key);

        if (config == null)
        {
            return ApiResult<SysConfigDto?>.Success(null);
        }

        return ApiResult<SysConfigDto?>.Success(MapToDto(config));
    }

    public async Task<ApiResult<SysConfigDto>> UpdateAsync(string key, SysConfigUpdateDto dto)
    {
        var config = await _context.Set<SysConfig>()
            .FirstOrDefaultAsync(c => c.FKey == key);

        if (config == null)
        {
            return ApiResult<SysConfigDto>.Fail($"配置项 '{key}' 不存在");
        }

        config.FValue = dto.Value;
        config.FUpdateTime = DateTime.Now;

        await _context.SaveChangesAsync();
        return ApiResult<SysConfigDto>.Success(MapToDto(config));
    }

    private static SysConfigDto MapToDto(SysConfig entity)
    {
        return new SysConfigDto
        {
            Id = entity.FID,
            Key = entity.FKey,
            Value = entity.FValue,
            DataType = entity.FDataType,
            Description = entity.FDescription,
            IsBuiltIn = entity.FIsBuiltIn
        };
    }
}
