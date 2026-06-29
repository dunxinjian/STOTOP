using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Services.Alert;

public class AlertConfigService : IAlertConfigService
{
    private readonly STOTOPDbContext _db;

    public AlertConfigService(STOTOPDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResult<PagedResult<AlertConfigDto>>> GetPagedAsync(long orgId, AlertConfigPagedRequest request)
    {
        var query = _db.Set<QlAlertConfig>().Where(e => e.FOrgId == orgId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(e => e.F配置名称.Contains(request.Keyword));
        if (!string.IsNullOrWhiteSpace(request.ThresholdType))
            query = query.Where(e => e.F阈值类型 == request.ThresholdType);
        if (request.Status.HasValue)
            query = query.Where(e => e.F状态 == request.Status.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.F创建时间)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new AlertConfigDto
            {
                Id = e.FID,
                Name = e.F配置名称,
                ThresholdType = e.F阈值类型,
                Threshold = e.F阈值,
                NotifyMethod = e.F通知方式,
                NotifyTargets = e.F通知对象,
                Status = e.F状态,
                CreateTime = e.F创建时间,
            })
            .ToListAsync();

        return ApiResult<PagedResult<AlertConfigDto>>.Success(new PagedResult<AlertConfigDto>
        {
            Items = items,
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
        });
    }

    public async Task<ApiResult<AlertConfigDto>> GetByIdAsync(long orgId, long id)
    {
        var entity = await _db.Set<QlAlertConfig>().FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (entity == null)
            return ApiResult<AlertConfigDto>.Fail("预警配置不存在");

        return ApiResult<AlertConfigDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult<AlertConfigDto>> CreateAsync(long orgId, CreateAlertConfigRequest request)
    {
        var entity = new QlAlertConfig
        {
            FOrgId = orgId,
            F配置名称 = request.Name,
            F阈值类型 = request.ThresholdType,
            F阈值 = request.Threshold,
            F通知方式 = request.NotifyMethod,
            F通知对象 = request.NotifyTargets,
            F状态 = 1,
            F创建时间 = DateTime.Now,
        };

        _db.Set<QlAlertConfig>().Add(entity);
        await _db.SaveChangesAsync();

        return ApiResult<AlertConfigDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult<AlertConfigDto>> UpdateAsync(long orgId, long id, UpdateAlertConfigRequest request)
    {
        var entity = await _db.Set<QlAlertConfig>().AsTracking()
            .FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (entity == null)
            return ApiResult<AlertConfigDto>.Fail("预警配置不存在");

        entity.F配置名称 = request.Name;
        entity.F阈值类型 = request.ThresholdType;
        entity.F阈值 = request.Threshold;
        entity.F通知方式 = request.NotifyMethod;
        entity.F通知对象 = request.NotifyTargets;

        await _db.SaveChangesAsync();

        return ApiResult<AlertConfigDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult<bool>> DeleteAsync(long orgId, long id)
    {
        var entity = await _db.Set<QlAlertConfig>().AsTracking()
            .FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (entity == null)
            return ApiResult<bool>.Fail("预警配置不存在");

        _db.Set<QlAlertConfig>().Remove(entity);
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true);
    }

    public async Task<ApiResult<bool>> ToggleAsync(long orgId, long id)
    {
        var entity = await _db.Set<QlAlertConfig>().AsTracking()
            .FirstOrDefaultAsync(e => e.FID == id && e.FOrgId == orgId);
        if (entity == null)
            return ApiResult<bool>.Fail("预警配置不存在");

        entity.F状态 = entity.F状态 == 1 ? 0 : 1;
        await _db.SaveChangesAsync();

        return ApiResult<bool>.Success(true);
    }

    private static AlertConfigDto MapToDto(QlAlertConfig entity)
    {
        return new AlertConfigDto
        {
            Id = entity.FID,
            Name = entity.F配置名称,
            ThresholdType = entity.F阈值类型,
            Threshold = entity.F阈值,
            NotifyMethod = entity.F通知方式,
            NotifyTargets = entity.F通知对象,
            Status = entity.F状态,
            CreateTime = entity.F创建时间,
        };
    }
}
