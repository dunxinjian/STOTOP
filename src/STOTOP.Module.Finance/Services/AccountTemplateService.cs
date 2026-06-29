using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class AccountTemplateService : IAccountTemplateService
{
    private readonly IRepository<FinAccountTemplate> _templateRepository;
    private readonly IRepository<FinAccountTemplateItem> _itemRepository;
    private readonly IRepository<FinAccount> _accountRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly STOTOPDbContext _dbContext;

    public AccountTemplateService(
        IRepository<FinAccountTemplate> templateRepository,
        IRepository<FinAccountTemplateItem> itemRepository,
        IRepository<FinAccount> accountRepository,
        IHttpContextAccessor httpContextAccessor,
        STOTOPDbContext dbContext)
    {
        _templateRepository = templateRepository;
        _itemRepository = itemRepository;
        _accountRepository = accountRepository;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    public async Task<List<AccountTemplateDto>> GetTemplatesAsync()
    {
        var currentOrgId = GetCurrentOrgId();
        var templates = await _templateRepository.Query()
            .Where(t => t.FOrgId == currentOrgId || t.FIsPreset == 1)
            .OrderBy(t => t.FCode)
            .ToListAsync();

        var templateIds = templates.Select(t => t.FID).ToList();

        // 批量查询每个模板的科目项数量
        var itemCounts = await _itemRepository.Query()
            .Where(i => templateIds.Contains(i.FTemplateId))
            .GroupBy(i => i.FTemplateId)
            .Select(g => new { TemplateId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countDict = itemCounts.ToDictionary(x => x.TemplateId, x => x.Count);

        return templates.Select(t => new AccountTemplateDto
        {
            Id = t.FID,
            Code = t.FCode,
            Name = t.FName,
            Description = t.FDescription,
            IsPreset = t.FIsPreset == 1,
            EnableStatus = t.FEnableStatus,
            ItemCount = countDict.GetValueOrDefault(t.FID, 0)
        }).ToList();
    }

    public async Task<AccountTemplateDetailDto> GetTemplateDetailAsync(long id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
            throw new InvalidOperationException("模板不存在");

        var items = await _itemRepository.Query()
            .Where(i => i.FTemplateId == id)
            .OrderBy(i => i.FSortOrder)
            .ThenBy(i => i.FCode)
            .ToListAsync();

        var dto = new AccountTemplateDetailDto
        {
            Id = template.FID,
            Code = template.FCode,
            Name = template.FName,
            Description = template.FDescription,
            IsPreset = template.FIsPreset == 1,
            EnableStatus = template.FEnableStatus,
            ItemCount = items.Count,
            Items = BuildItemTree(items)
        };

        return dto;
    }

    public async Task<long> CreateTemplateAsync(CreateAccountTemplateRequest request)
    {
        // 检查编码唯一性
        var existing = await _templateRepository.Query()
            .FirstOrDefaultAsync(t => t.FCode == request.Code);
        if (existing != null)
            throw new InvalidOperationException($"模板编码 {request.Code} 已存在");

        var template = new FinAccountTemplate
        {
            FCode = request.Code,
            FName = request.Name,
            FDescription = request.Description,
            FIsPreset = 0,
            FEnableStatus = 1,
            FOrgId = GetCurrentOrgId(),
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _templateRepository.AddAsync(template);
        return template.FID;
    }

    public async Task UpdateTemplateAsync(long id, UpdateAccountTemplateRequest request)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
            throw new InvalidOperationException("模板不存在");

        template.FName = request.Name;
        template.FDescription = request.Description;
        template.FUpdatedTime = DateTime.Now;

        await _templateRepository.UpdateAsync(template);
    }

    public async Task DeleteTemplateAsync(long id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
            throw new InvalidOperationException("模板不存在");

        if (template.FIsPreset == 1)
            throw new InvalidOperationException("预置模板不允许删除");

        // 级联删除明细项
        var items = await _itemRepository.Query()
            .Where(i => i.FTemplateId == id)
            .ToListAsync();

        foreach (var item in items)
        {
            await _itemRepository.DeleteAsync(item.FID);
        }

        await _templateRepository.DeleteAsync(id);
    }

    public async Task<List<AccountTemplateItemTreeDto>> GetTemplateItemsTreeAsync(long id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
            throw new InvalidOperationException("模板不存在");

        var items = await _itemRepository.Query()
            .Where(i => i.FTemplateId == id)
            .OrderBy(i => i.FSortOrder)
            .ThenBy(i => i.FCode)
            .ToListAsync();

        return BuildItemTree(items);
    }

    public async Task<AccountTemplateItemDto> AddTemplateItemAsync(long templateId, CreateTemplateItemRequest request)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null)
            throw new InvalidOperationException("模板不存在");

        // 检查编码唯一性
        var existing = await _itemRepository.Query()
            .FirstOrDefaultAsync(i => i.FTemplateId == templateId && i.FCode == request.Code);
        if (existing != null)
            throw new InvalidOperationException($"科目编码 {request.Code} 在该模板中已存在");

        // 计算级次
        int level = 1;
        if (request.ParentId > 0)
        {
            var parent = await _itemRepository.GetByIdAsync(request.ParentId);
            if (parent != null && parent.FTemplateId == templateId)
            {
                level = parent.FLevel + 1;
                // 更新父项为非末级
                parent.FIsLeaf = 0;
                await _itemRepository.UpdateAsync(parent);
            }
        }

        // 计算排序号
        var maxSort = await _itemRepository.Query()
            .Where(i => i.FTemplateId == templateId)
            .Select(i => (int?)i.FSortOrder)
            .MaxAsync() ?? 0;

        var item = new FinAccountTemplateItem
        {
            FTemplateId = templateId,
            FCode = request.Code,
            FName = request.Name,
            FCategory = request.Category,
            FBalanceDirection = request.BalanceDirection,
            FLevel = level,
            FParentId = request.ParentId,
            FIsLeaf = 1, // 新增的科目默认为末级
            FAuxiliary = request.Auxiliary,
            FCurrency = request.Currency,
            FUnit = request.Unit,
            FSortOrder = maxSort + 1
        };

        await _itemRepository.AddAsync(item);
        return MapItemToDto(item);
    }

    public async Task UpdateTemplateItemAsync(long templateId, long itemId, UpdateTemplateItemRequest request)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item == null || item.FTemplateId != templateId)
            throw new InvalidOperationException("科目项不存在");

        item.FName = request.Name;
        item.FCategory = request.Category;
        item.FBalanceDirection = request.BalanceDirection;
        item.FAuxiliary = request.Auxiliary;
        item.FCurrency = request.Currency;
        item.FUnit = request.Unit;

        await _itemRepository.UpdateAsync(item);
    }

    public async Task DeleteTemplateItemAsync(long templateId, long itemId)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item == null || item.FTemplateId != templateId)
            throw new InvalidOperationException("科目项不存在");

        // 级联删除所有子项
        await DeleteChildrenRecursiveAsync(templateId, itemId);

        // 删除本项
        await _itemRepository.DeleteAsync(itemId);

        // 更新父项的是否末级状态
        if (item.FParentId > 0)
        {
            var siblings = await _itemRepository.Query()
                .Where(i => i.FTemplateId == templateId && i.FParentId == item.FParentId)
                .ToListAsync();

            if (!siblings.Any())
            {
                var parent = await _itemRepository.GetByIdAsync(item.FParentId);
                if (parent != null)
                {
                    parent.FIsLeaf = 1;
                    await _itemRepository.UpdateAsync(parent);
                }
            }
        }
    }

    /// <summary>
    /// 将模板科目应用到指定账套
    /// </summary>
    public async Task ApplyTemplateAsync(long templateId, long accountSetId)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null)
            throw new InvalidOperationException("模板不存在");

        // 读取模板所有明细项，按级次排序确保父项先创建
        var items = await _itemRepository.Query()
            .Where(i => i.FTemplateId == templateId)
            .OrderBy(i => i.FLevel)
            .ThenBy(i => i.FCode)
            .ToListAsync();

        if (!items.Any())
            throw new InvalidOperationException("模板中没有科目数据");

        var currentOrgId = GetCurrentOrgId();

        // 使用事务保证所有科目创建的原子性
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // 模板项FID -> 新科目FID 的映射
            var idMapping = new Dictionary<long, long>();

            foreach (var item in items)
            {
                // 处理父子关系映射
                long newParentId = 0;
                if (item.FParentId > 0)
                {
                    if (idMapping.TryGetValue(item.FParentId, out var mappedParentId))
                    {
                        newParentId = mappedParentId;
                    }
                    // 如果找不到映射，保持0（顶级）
                }

                var account = new FinAccount
                {
                    FCode = item.FCode,
                    FName = item.FName,
                    FCategory = item.FCategory,
                    FBalanceDirection = item.FBalanceDirection,
                    FLevel = item.FLevel,
                    FParentId = newParentId,
                    FIsLeaf = item.FIsLeaf,
                    FAuxiliary = item.FAuxiliary,
                    FCurrency = item.FCurrency,
                    FUnit = item.FUnit,
                    FEnableStatus = 1,
                    FAccountSetId = accountSetId,
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };

                await _accountRepository.AddAsync(account);
                idMapping[item.FID] = account.FID;
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 递归删除子项
    /// </summary>
    private async Task DeleteChildrenRecursiveAsync(long templateId, long parentId)
    {
        var children = await _itemRepository.Query()
            .Where(i => i.FTemplateId == templateId && i.FParentId == parentId)
            .ToListAsync();

        foreach (var child in children)
        {
            await DeleteChildrenRecursiveAsync(templateId, child.FID);
            await _itemRepository.DeleteAsync(child.FID);
        }
    }

    /// <summary>
    /// 构建科目项树形结构
    /// </summary>
    private static List<AccountTemplateItemTreeDto> BuildItemTree(List<FinAccountTemplateItem> items)
    {
        var lookup = items.ToLookup(i => i.FParentId);

        AccountTemplateItemTreeDto BuildNode(FinAccountTemplateItem item)
        {
            var node = new AccountTemplateItemTreeDto
            {
                Id = item.FID,
                TemplateId = item.FTemplateId,
                Code = item.FCode,
                Name = item.FName,
                Category = item.FCategory,
                BalanceDirection = item.FBalanceDirection,
                Level = item.FLevel,
                ParentId = item.FParentId,
                IsLeaf = item.FIsLeaf == 1,
                Auxiliary = item.FAuxiliary,
                Currency = item.FCurrency,
                Unit = item.FUnit,
                SortOrder = item.FSortOrder
            };

            node.Children = lookup[item.FID].Select(BuildNode).ToList();
            return node;
        }

        return lookup[0].Select(BuildNode).ToList();
    }

    private static AccountTemplateItemDto MapItemToDto(FinAccountTemplateItem item)
    {
        return new AccountTemplateItemDto
        {
            Id = item.FID,
            TemplateId = item.FTemplateId,
            Code = item.FCode,
            Name = item.FName,
            Category = item.FCategory,
            BalanceDirection = item.FBalanceDirection,
            Level = item.FLevel,
            ParentId = item.FParentId,
            IsLeaf = item.FIsLeaf == 1,
            Auxiliary = item.FAuxiliary,
            Currency = item.FCurrency,
            Unit = item.FUnit,
            SortOrder = item.FSortOrder
        };
    }
}
