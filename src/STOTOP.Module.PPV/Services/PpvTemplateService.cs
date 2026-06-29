using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.PPV.Dtos;
using STOTOP.Module.PPV.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.PPV.Services;

public class PpvTemplateService : IPpvTemplateService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<PpvTemplateService> _logger;

    public PpvTemplateService(STOTOPDbContext context, ILogger<PpvTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResult<List<PpvTemplateDto>>> GetListAsync(long orgId, int page, int pageSize, long? positionId)
    {
        var query = _context.Set<PpvTemplate>()
            .Where(t => t.FOrgId == orgId);

        if (positionId.HasValue)
            query = query.Where(t => t.F岗位ID == positionId.Value);

        var list = await query
            .OrderByDescending(t => t.FID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var positionIds = list.Select(t => t.F岗位ID).Distinct().ToList();
        var positions = await _context.Set<SysPosition>()
            .Where(p => positionIds.Contains(p.FID))
            .Select(p => new { p.FID, p.FName })
            .ToListAsync();
        var positionMap = positions.ToDictionary(p => p.FID, p => p.FName);

        var dtos = list.Select(t =>
        {
            var dto = MapToDto(t);
            dto.PositionName = positionMap.GetValueOrDefault(t.F岗位ID);
            return dto;
        }).ToList();

        return ApiResult<List<PpvTemplateDto>>.Success(dtos);
    }

    public async Task<ApiResult<PpvTemplateDto>> CreateAsync(long orgId, CreatePpvTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResult<PpvTemplateDto>.Fail("模板名称不能为空");
        if (string.IsNullOrWhiteSpace(request.ItemCode))
            return ApiResult<PpvTemplateDto>.Fail("产值项编码不能为空");

        var entity = new PpvTemplate
        {
            FOrgId = orgId,
            F名称 = request.Name.Trim(),
            F岗位ID = request.PositionId,
            F产值项编码 = request.ItemCode.Trim(),
            F产值项名称 = request.ItemName.Trim(),
            F单价 = request.UnitPrice,
            F计量单位 = request.Unit,
            F启用状态 = request.IsEnabled,
            F生效起期 = request.EffectiveFrom,
            F生效止期 = request.EffectiveTo,
            F创建时间 = DateTime.Now,
            F更新时间 = DateTime.Now
        };

        _context.Set<PpvTemplate>().Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[PPV] 创建模板 Id={Id} OrgId={OrgId} ItemCode={ItemCode}",
            entity.FID, orgId, entity.F产值项编码);

        return ApiResult<PpvTemplateDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult<PpvTemplateDto>> UpdateAsync(long orgId, long id, UpdatePpvTemplateRequest request)
    {
        var entity = await _context.Set<PpvTemplate>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id && t.FOrgId == orgId);
        if (entity == null)
            return ApiResult<PpvTemplateDto>.Fail("模板不存在");

        entity.F名称 = request.Name.Trim();
        entity.F岗位ID = request.PositionId;
        entity.F产值项编码 = request.ItemCode.Trim();
        entity.F产值项名称 = request.ItemName.Trim();
        entity.F单价 = request.UnitPrice;
        entity.F计量单位 = request.Unit;
        entity.F启用状态 = request.IsEnabled;
        entity.F生效起期 = request.EffectiveFrom;
        entity.F生效止期 = request.EffectiveTo;
        entity.F更新时间 = DateTime.Now;

        await _context.SaveChangesAsync();
        return ApiResult<PpvTemplateDto>.Success(MapToDto(entity));
    }

    public async Task<ApiResult> EnableAsync(long orgId, long id)
    {
        var entity = await _context.Set<PpvTemplate>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.FID == id && t.FOrgId == orgId);
        if (entity == null)
            return ApiResult.Fail("模板不存在");

        var newState = !entity.F启用状态;

        // 启用时校验：同组织+同岗位+同产值项编码+同生效期间，仅允许一个启用
        if (newState)
        {
            var conflict = await _context.Set<PpvTemplate>()
                .Where(t => t.FOrgId == orgId
                    && t.F岗位ID == entity.F岗位ID
                    && t.F产值项编码 == entity.F产值项编码
                    && t.F启用状态
                    && t.FID != id
                    && t.F生效起期 == entity.F生效起期)
                .AnyAsync();
            if (conflict)
                return ApiResult.Fail("同岗位同产值项编码在该生效期间已有启用模板");
        }

        entity.F启用状态 = newState;
        entity.F更新时间 = DateTime.Now;
        await _context.SaveChangesAsync();

        return ApiResult.Ok(newState ? "已启用" : "已停用");
    }

    public async Task<ApiResult<List<PpvTemplateDto>>> GetByPositionAsync(long orgId, long positionId)
    {
        var list = await _context.Set<PpvTemplate>()
            .Where(t => t.FOrgId == orgId && t.F岗位ID == positionId && t.F启用状态)
            .OrderBy(t => t.F产值项编码)
            .ToListAsync();

        var dtos = list.Select(MapToDto).ToList();
        return ApiResult<List<PpvTemplateDto>>.Success(dtos);
    }

    private static PpvTemplateDto MapToDto(PpvTemplate t) => new()
    {
        Id = t.FID,
        OrgId = t.FOrgId,
        Name = t.F名称,
        PositionId = t.F岗位ID,
        ItemCode = t.F产值项编码,
        ItemName = t.F产值项名称,
        UnitPrice = t.F单价,
        Unit = t.F计量单位,
        IsEnabled = t.F启用状态,
        EffectiveFrom = t.F生效起期,
        EffectiveTo = t.F生效止期,
        CreateTime = t.F创建时间,
        UpdateTime = t.F更新时间
    };
}
