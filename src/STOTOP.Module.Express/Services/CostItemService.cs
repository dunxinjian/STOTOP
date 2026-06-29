using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 成本项目服务
/// </summary>
public class CostItemService : ICostItemService
{
    private readonly IRepository<ExpCostItem> _repository;

    public CostItemService(IRepository<ExpCostItem> repository)
    {
        _repository = repository;
    }

    public async Task<List<CostItemDto>> GetAllAsync()
    {
        var items = await _repository.Query()
            .OrderBy(e => e.FSortOrder)
            .ToListAsync();

        return items.Select(e => new CostItemDto
        {
            Id = e.FID,
            Code = e.FCode,
            Name = e.FName,
            IsRebate = e.FIsRebate,
            SortOrder = e.FSortOrder
        }).ToList();
    }

    public async Task<CostItemDto> CreateAsync(CreateCostItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("成本项目编码不能为空");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("成本项目名称不能为空");

        var code = request.Code.Trim();
        var exists = await _repository.Query().AnyAsync(e => e.FCode == code);
        if (exists)
            throw new InvalidOperationException($"成本项目编码「{code}」已存在");

        var entity = new ExpCostItem
        {
            FCode = code,
            FName = request.Name.Trim(),
            FIsRebate = request.IsRebate,
            FSortOrder = request.SortOrder
        };
        var saved = await _repository.AddAsync(entity);
        return new CostItemDto
        {
            Id = saved.FID,
            Code = saved.FCode,
            Name = saved.FName,
            IsRebate = saved.FIsRebate,
            SortOrder = saved.FSortOrder
        };
    }

    public async Task<CostItemDto?> UpdateAsync(int id, UpdateCostItemRequest request)
    {
        var entity = await _repository.Query().FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null) return null;

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("成本项目名称不能为空");

        entity.FName = request.Name.Trim();
        entity.FIsRebate = request.IsRebate;
        entity.FSortOrder = request.SortOrder;
        await _repository.UpdateAsync(entity);

        return new CostItemDto
        {
            Id = entity.FID,
            Code = entity.FCode,
            Name = entity.FName,
            IsRebate = entity.FIsRebate,
            SortOrder = entity.FSortOrder
        };
    }
}
