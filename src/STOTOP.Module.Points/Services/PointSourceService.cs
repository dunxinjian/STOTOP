using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Services;

public class PointSourceService : IPointSourceService
{
    private readonly STOTOPDbContext _db;

    public PointSourceService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<List<PointSourceDto>>> GetListAsync(long orgId)
    {
        var sources = await _db.Set<PmPointSource>()
            .Where(s => s.FOrgId == orgId)
            .OrderBy(s => s.FSortOrder)
            .ToListAsync();

        var items = sources.Select(MapToDto).ToList();
        return ApiResult<List<PointSourceDto>>.Success(items);
    }

    public async Task<ApiResult<PointSourceDto>> CreateAsync(long orgId, CreatePointSourceRequest request)
    {
        // 检查编码唯一性
        var exists = await _db.Set<PmPointSource>()
            .AnyAsync(s => s.FOrgId == orgId && s.FSourceCode == request.SourceCode);
        if (exists)
            return ApiResult<PointSourceDto>.Fail("来源编码已存在");

        var entity = new PmPointSource
        {
            FOrgId = orgId,
            FSourceName = request.SourceName,
            FSourceCode = request.SourceCode,
            FIcon = request.Icon,
            FColor = request.Color,
            FDescription = request.Description,
            FSortOrder = request.SortOrder,
            FIsEnabled = true
        };

        _db.Set<PmPointSource>().Add(entity);
        await _db.SaveChangesAsync();

        return ApiResult<PointSourceDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult<PointSourceDto>> UpdateAsync(long id, UpdatePointSourceRequest request)
    {
        var entity = await _db.Set<PmPointSource>()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (entity == null)
            return ApiResult<PointSourceDto>.Fail("积分来源不存在");

        entity.FSourceName = request.SourceName;
        entity.FIcon = request.Icon;
        entity.FColor = request.Color;
        entity.FDescription = request.Description;
        entity.FSortOrder = request.SortOrder;
        entity.FIsEnabled = request.IsEnabled;

        await _db.SaveChangesAsync();
        return ApiResult<PointSourceDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult<bool>> ToggleAsync(long id)
    {
        var entity = await _db.Set<PmPointSource>()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.FID == id);

        if (entity == null)
            return ApiResult<bool>.Fail("积分来源不存在");

        entity.FIsEnabled = !entity.FIsEnabled;
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true, entity.FIsEnabled ? "已启用" : "已禁用");
    }

    private static PointSourceDto MapToDto(PmPointSource e) => new()
    {
        Id = e.FID,
        OrgId = e.FOrgId,
        SourceName = e.FSourceName,
        SourceCode = e.FSourceCode,
        Icon = e.FIcon,
        Color = e.FColor,
        Description = e.FDescription,
        SortOrder = e.FSortOrder,
        IsEnabled = e.FIsEnabled
    };
}
