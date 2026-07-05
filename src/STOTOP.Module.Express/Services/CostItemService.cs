using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Services.Billing;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 成本项目服务
/// </summary>
public class CostItemService : ICostItemService
{
    private readonly IRepository<ExpCostItem> _repository;
    private readonly IRepository<ExpCostPlanItem> _planItemRepo;

    public CostItemService(IRepository<ExpCostItem> repository, IRepository<ExpCostPlanItem> planItemRepo)
    {
        _repository = repository;
        _planItemRepo = planItemRepo;
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

        // 计费侧靠"全局成本项目名称 ↔ 方案成本项名称"（规范化后忽略大小写/空白）匹配返利标志与编码索引，两表无外键。
        // 若改名导致规范化名称变化且旧名被方案成本项引用，匹配会被静默切断（返利被当正向成本、算错钱），故拦截。
        var newName = request.Name.Trim();
        var oldNorm = CostPlanCache.NormalizeItemName(entity.FName);
        var newNorm = CostPlanCache.NormalizeItemName(newName);
        if (!string.Equals(oldNorm, newNorm, StringComparison.OrdinalIgnoreCase))
        {
            var referenced = (await _planItemRepo.Query().Select(i => i.FItemName).ToListAsync())
                .Any(n => string.Equals(CostPlanCache.NormalizeItemName(n), oldNorm, StringComparison.OrdinalIgnoreCase));
            if (referenced)
                throw new InvalidOperationException(
                    $"成本项目「{entity.FName}」已被成本方案项按名称引用，改名会切断返利/编码标志匹配（导致返利被当正向成本算错），请先调整引用的方案成本项名称后再改名");
        }

        entity.FName = newName;
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
