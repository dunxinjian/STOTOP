using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.System.Services.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.Express.Services;

public class BrandService : IBrandService
{
    private readonly IRepository<ExpBrand> _repository;
    private readonly STOTOPDbContext _dbContext;
    private readonly ICodeRuleService _codeRuleService;
    private readonly IEventDispatcher _eventDispatcher;

    public BrandService(IRepository<ExpBrand> repository, STOTOPDbContext dbContext, ICodeRuleService codeRuleService, IEventDispatcher eventDispatcher)
    {
        _repository = repository;
        _dbContext = dbContext;
        _codeRuleService = codeRuleService;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<PagedResult<BrandListItemDto>> GetListAsync(BrandQueryRequest request)
    {
        var query = _repository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e => e.FCode.Contains(keyword) || e.FName.Contains(keyword));
        }

        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<BrandListItemDto>
        {
            Items = items.Select(MapToListItemDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<BrandDto?> GetByCodeAsync(string code)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<BrandDto> CreateAsync(CreateBrandRequest request)
    {
        // EXP_BRAND 编码规则已废弃，品牌编码改为人工录入
        var code = (request.Code ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(code))
            throw new ArgumentException("品牌编码不能为空", nameof(request.Code));

        var entity = new ExpBrand
        {
            FCode = code,
            FName = request.Name,
            FStatus = request.Status,
            FRemark = request.Remark,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        var result = await _repository.AddAsync(entity);
        return MapToDto(result);
    }

    public async Task<BrandDto?> UpdateAsync(string code, UpdateBrandRequest request)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        if (entity == null) return null;

        var oldName = entity.FName;

        entity.FName = request.Name;
        entity.FStatus = request.Status;
        entity.FRemark = request.Remark;
        entity.FUpdatedTime = DateTime.Now;

        await _repository.UpdateAsync(entity);

        // 名称变更时发布辅助核算同步事件
        if (oldName != request.Name)
        {
            await _eventDispatcher.PublishAsync(new AuxiliarySourceChangedEvent
            {
                SourceType = "EXP品牌",
                SourceId = 0, // 主键已改为编码，此处传0兼容
                NewName = request.Name
            });
        }

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string code)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FCode == code);
        if (entity == null) return false;

        _dbContext.Set<ExpBrand>().Remove(entity);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static BrandDto MapToDto(ExpBrand e) => new()
    {
        Code = e.FCode,
        Name = e.FName,
        Status = e.FStatus,
        Remark = e.FRemark,
        CreatedTime = e.FCreatedTime,
        UpdatedTime = e.FUpdatedTime
    };

    private static BrandListItemDto MapToListItemDto(ExpBrand e) => new()
    {
        Code = e.FCode,
        Name = e.FName,
        Status = e.FStatus,
        CreatedTime = e.FCreatedTime
    };
}
