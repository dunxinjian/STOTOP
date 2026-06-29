using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class AmoebaService : IAmoebaService
{
    private readonly IRepository<FinAmoebaPLTemplate> _templateRepository;
    private readonly IRepository<FinAmoebaPLItem> _itemRepository;
    private readonly STOTOPDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AmoebaService(
        IRepository<FinAmoebaPLTemplate> templateRepository,
        IRepository<FinAmoebaPLItem> itemRepository,
        STOTOPDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _templateRepository = templateRepository;
        _itemRepository = itemRepository;
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 在事务中执行多步写入（仓储 AddAsync/UpdateAsync 共享同一 scoped DbContext，会登记到环境事务）。
    /// 已启用 EnableRetryOnFailure，用户事务必须经 ExecutionStrategy 包裹；
    /// 非关系型提供程序（单测 InMemory）不支持事务，直接执行。
    /// </summary>
    private async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        if (!_dbContext.Database.IsRelational())
        {
            await action();
            return;
        }
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            await action();
            await transaction.CommitAsync();
        });
    }

    #region PL Template

    public async Task<List<AmoebaPLTemplateDto>> GetTemplatesAsync(long? accountSetId = null)
    {
        var query = _templateRepository.Query().AsQueryable();
        
        if (accountSetId.HasValue && accountSetId.Value > 0)
        {
            query = query.Where(t => t.FAccountSetId == accountSetId.Value);
        }

        var templates = await query.Include(t => t.Items).ToListAsync();
        return templates.Select(MapTemplateToDto).ToList();
    }

    public async Task<AmoebaPLTemplateDto?> GetTemplateByIdAsync(long id)
    {
        var template = await _templateRepository.Query()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.FID == id);
        
        return template == null ? null : MapTemplateToDto(template);
    }

    public async Task<AmoebaPLTemplateDto> CreateTemplateAsync(CreateAmoebaPLTemplateRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("模板名称不能为空", nameof(request.Name));
        }
        var template = new FinAmoebaPLTemplate
        {
            FName = name,
            FDescription = request.Description,
            FAccountSetId = request.AccountSetId,
            FIsDefault = 0,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _templateRepository.AddAsync(template);
        return MapTemplateToDto(template);
    }

    public async Task<AmoebaPLTemplateDto?> UpdateTemplateAsync(long id, UpdateAmoebaPLTemplateRequest request)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null) return null;

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("模板名称不能为空", nameof(request.Name));
        }
        template.FName = name;
        template.FDescription = request.Description;
        template.FUpdatedTime = DateTime.Now;
        
        await _templateRepository.UpdateAsync(template);
        return MapTemplateToDto(template);
    }

    public async Task<bool> DeleteTemplateAsync(long id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null) return false;

        if (template.FIsDefault == 1)
        {
            throw new InvalidOperationException("默认模板不能删除");
        }

        await _templateRepository.DeleteAsync(id);
        return true;
    }

    /// <summary>
    /// 从现有模板复制创建新模板：复制模板基本信息（名称用新值、账套用新值），
    /// 深拷贝所有损益项并保持父子层级，重建 FParentId 映射。
    /// 不复制手工填报数据（FinAmoebaManualData）。
    /// </summary>
    public async Task<AmoebaPLTemplateDto> CloneTemplateAsync(long sourceId, CloneAmoebaPLTemplateRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("模板名称不能为空", nameof(request.Name));
        }

        var source = await _templateRepository.Query()
            .FirstOrDefaultAsync(t => t.FID == sourceId);
        if (source == null)
        {
            throw new InvalidOperationException("源模板不存在");
        }

        // 1. 查源模板所有损益项（读操作在事务外）
        var sourceItems = await _itemRepository.Query()
            .Where(i => i.FTemplateId == sourceId)
            .OrderBy(i => i.FSort)
            .ThenBy(i => i.FID)
            .ToListAsync();

        // 2. 创建新模板 + 按层级深拷贝（先父后子，保证 FParentId 映射可用）。
        // 整体事务化：仓储逐条提交，中途失败（如异常数据）不留半个模板
        var newTemplate = new FinAmoebaPLTemplate
        {
            FName = name,
            FDescription = request.Description ?? source.FDescription,
            FAccountSetId = request.AccountSetId,
            FIsDefault = 0,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await ExecuteInTransactionAsync(async () =>
        {
            await _templateRepository.AddAsync(newTemplate);

            var idMap = new Dictionary<long, long> { { 0L, 0L } };
            var pending = new List<FinAmoebaPLItem>(sourceItems);
            var now = DateTime.Now;
            var safety = pending.Count + 1; // 防御循环异常
            while (pending.Count > 0 && safety-- > 0)
            {
                var ready = pending.Where(i => idMap.ContainsKey(i.FParentId)).ToList();
                if (ready.Count == 0) break; // 存在孤立节点（环/断链脏数据），它们在任何视图都不可见，跳过不复制
                foreach (var item in ready)
                {
                    var newItem = new FinAmoebaPLItem
                    {
                        FTemplateId = newTemplate.FID,
                        FParentId = idMap[item.FParentId],
                        FItemName = item.FItemName,
                        FNodeRole = item.FNodeRole,
                        FFormula = item.FFormula,
                        FSort = item.FSort,
                        FRelatedAccountsJson = item.FRelatedAccountsJson,
                        FDataSource = item.FDataSource,
                        FSummaryKeywordsJson = item.FSummaryKeywordsJson,
                        FBillingFilterJson = item.FBillingFilterJson,
                        FUnit = item.FUnit,
                        FDataSourceRemark = item.FDataSourceRemark,
                        FCalculationLogic = item.FCalculationLogic,
                        FPerUnitMode = item.FPerUnitMode,
                        F小数位数 = item.F小数位数,
                        FIsManualEntry = item.FIsManualEntry,
                        FCreatedTime = now,
                        FUpdatedTime = now,
                        // 正交二维字段（新增）
                        F项目类别 = item.F项目类别,
                        F值来源 = item.F值来源,
                        F系统数据源 = item.F系统数据源,
                        F是否指标分区 = item.F是否指标分区,
                    };
                    await _itemRepository.AddAsync(newItem);
                    idMap[item.FID] = newItem.FID;
                    pending.Remove(item);
                }
            }
        });

        // 4. 重新加载新模板（含 Items），按现有 Mapping 返回
        var created = await _templateRepository.Query()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.FID == newTemplate.FID);
        return created == null ? MapTemplateToDto(newTemplate) : MapTemplateToDto(created);
    }

    #endregion

    #region PL Item

    public async Task<AmoebaPLItemDto> AddItemAsync(long templateId, CreateAmoebaPLItemRequest request)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null)
        {
            throw new InvalidOperationException("模板不存在");
        }

        // 名称统一 trim 落库：唯一性校验与公式 ${名称} 引用都按 trim 后比较，
        // 带首尾空格的名称会绕过唯一性且公式永远匹配不上
        var itemName = (request.ItemName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(itemName))
        {
            throw new InvalidOperationException("项目名称不能为空");
        }

        // 名称唯一性校验：同模板下所有 PLItem 的 FItemName 不得重复。
        // indicator 节点除外（根据产品设计：指标项不作为公式引用目标）。
        if (!string.Equals(request.NodeRole, "indicator", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureItemNameUniqueAsync(templateId, itemName, excludeItemId: null);
        }

        if (request.ParentId > 0)
        {
            var parentItem = await _itemRepository.Query()
                .FirstOrDefaultAsync(i => i.FID == request.ParentId && i.FTemplateId == templateId);
            // 父级必须真实存在，否则会落库为树上不可见、却占用名称唯一性的孤儿项
            if (parentItem == null)
            {
                throw new InvalidOperationException("所属父级不存在，请重新选择");
            }
            // 指标分区验证：若父项是指标分区，子项的 ItemCategory 必须为 "indicator"。
            // 仅认根级标记——V4 迁移前嵌套分组可能残留误标，按普通分组对待
            if (parentItem.F是否指标分区 && parentItem.FParentId == 0 && request.ItemCategory != "indicator")
            {
                throw new InvalidOperationException("指标分区的子项的项目类别必须为 indicator");
            }
        }

        var item = new FinAmoebaPLItem
        {
            FTemplateId = templateId,
            FItemName = itemName,
            FNodeRole = request.NodeRole,
            FFormula = request.Formula,
            FSort = request.Sort,
            FParentId = request.ParentId,
            FRelatedAccountsJson = request.RelatedAccountsJson,
            FDataSource = request.DataSource,
            FSummaryKeywordsJson = request.SummaryKeywordsJson,
            FUnit = request.Unit,
            FDataSourceRemark = request.DataSourceRemark,
            FCalculationLogic = request.CalculationLogic,
            FPerUnitMode = request.PerUnitMode,
            F小数位数 = request.DecimalPlaces,
            FIsManualEntry = request.IsManualEntry,
            FBillingFilterJson = request.BillingFilterJson,
            F项目类别 = request.ItemCategory,
            F值来源 = request.ValueSource,
            F系统数据源 = request.SystemDataSource,
            F是否指标分区 = request.IsIndicatorSection,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        await _itemRepository.AddAsync(item);
        return MapItemToDto(item);
    }

    public async Task<AmoebaPLItemDto?> UpdateItemAsync(long templateId, long itemId, UpdateAmoebaPLItemRequest request)
    {
        var item = await _itemRepository.Query()
            .FirstOrDefaultAsync(i => i.FID == itemId && i.FTemplateId == templateId);
        
        if (item == null) return null;

        // 名称唯一性校验（排除本记录与 indicator 类型项）。
        var effectiveRole = !string.IsNullOrWhiteSpace(request.NodeRole) ? request.NodeRole : item.FNodeRole;
        if (!string.Equals(effectiveRole, "indicator", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureItemNameUniqueAsync(templateId, request.ItemName, excludeItemId: itemId);
        }

        // 移动校验：父级须存在、不能是自身或自身后代（防环）、指标分区下只能放 indicator 子项
        if (request.ParentId.HasValue && request.ParentId.Value != item.FParentId)
        {
            await EnsureValidParentAsync(templateId, item, request.ParentId.Value, request.ItemCategory);
        }
        else if (item.FParentId > 0 && request.ItemCategory != "indicator")
        {
            // 原地更新同样不得把指标分区子项的类别改为非 indicator（指标分区仅认根级标记）
            var currentParent = await _itemRepository.Query()
                .FirstOrDefaultAsync(i => i.FID == item.FParentId && i.FTemplateId == templateId);
            if (currentParent?.F是否指标分区 == true && currentParent.FParentId == 0)
            {
                throw new InvalidOperationException("指标分区的子项的项目类别必须为 indicator");
            }
        }

        var oldName = item.FItemName;
        var newName = (request.ItemName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(newName))
        {
            throw new InvalidOperationException("项目名称不能为空");
        }
        item.FItemName = newName;
        if (!string.IsNullOrWhiteSpace(request.NodeRole))
        {
            item.FNodeRole = request.NodeRole;
        }
        item.FFormula = request.Formula;
        item.FSort = request.Sort;
        if (request.ParentId.HasValue)
        {
            item.FParentId = request.ParentId.Value;
        }
        item.FRelatedAccountsJson = request.RelatedAccountsJson;
        item.FDataSource = request.DataSource;
        item.FSummaryKeywordsJson = request.SummaryKeywordsJson;
        item.FUnit = request.Unit;
        item.FDataSourceRemark = request.DataSourceRemark;
        item.FCalculationLogic = request.CalculationLogic;
        item.FPerUnitMode = request.PerUnitMode;
        if (request.DecimalPlaces.HasValue)
        {
            item.F小数位数 = request.DecimalPlaces.Value;
        }
        else
        {
            item.F小数位数 = null;
        }
        if (request.IsManualEntry.HasValue)
        {
            item.FIsManualEntry = request.IsManualEntry.Value;
        }
        item.FBillingFilterJson = request.BillingFilterJson;
        // 正交二维字段（新增）
        item.F项目类别 = request.ItemCategory;
        item.F值来源 = request.ValueSource;
        item.F系统数据源 = request.SystemDataSource;
        if (request.IsIndicatorSection.HasValue)
        {
            item.F是否指标分区 = request.IsIndicatorSection.Value;
        }
        // 自动数据源强制关闭手工填报标记
        var autoDataSources = new[] { "billing", "voucher", "depreciation", "estimate", "allocation" };
        if (!string.IsNullOrEmpty(item.FDataSource) && autoDataSources.Contains(item.FDataSource))
        {
            item.FIsManualEntry = false;
        }
        item.FUpdatedTime = DateTime.Now;

        // 改名时同步重写同模板公式中的 ${旧名} 引用（与本次更新同事务），引用方公式不再静默归 0
        var renamed = !string.Equals(oldName, newName, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(oldName);
        List<FinAmoebaPLItem> referencingItems = new();
        if (renamed)
        {
            var oldToken = "${" + oldName + "}";
            referencingItems = await _itemRepository.Query()
                .Where(i => i.FTemplateId == templateId && i.FID != itemId
                            && i.FFormula != null && i.FFormula.Contains(oldToken))
                .ToListAsync();
        }

        await ExecuteInTransactionAsync(async () =>
        {
            await _itemRepository.UpdateAsync(item);
            foreach (var refItem in referencingItems)
            {
                refItem.FFormula = refItem.FFormula!.Replace("${" + oldName + "}", "${" + newName + "}");
                refItem.FUpdatedTime = DateTime.Now;
                await _itemRepository.UpdateAsync(refItem);
            }
        });
        return MapItemToDto(item);
    }

    public async Task<bool> DeleteItemAsync(long templateId, long itemId)
    {
        var item = await _itemRepository.Query()
            .FirstOrDefaultAsync(i => i.FID == itemId && i.FTemplateId == templateId);

        if (item == null) return false;

        var allItems = await _itemRepository.Query()
            .Where(i => i.FTemplateId == templateId)
            .ToListAsync();
        var idsToDelete = new List<long>();
        CollectDescendantIds(itemId, allItems, idsToDelete);
        idsToDelete.Add(itemId);
        var deleteIdSet = idsToDelete.Distinct().ToHashSet();

        // 删除保护：若模板内其他项的公式仍引用被删项（含其子孙）名称，阻止删除并明确告知，
        // 否则引用方公式会静默归 0
        var deletedNames = allItems
            .Where(i => deleteIdSet.Contains(i.FID) && !string.IsNullOrEmpty(i.FItemName))
            .Select(i => i.FItemName)
            .ToHashSet();
        var blockers = allItems
            .Where(i => !deleteIdSet.Contains(i.FID) && !string.IsNullOrWhiteSpace(i.FFormula))
            .Where(i => deletedNames.Any(name => i.FFormula!.Contains("${" + name + "}")))
            .Select(i => i.FItemName)
            .Distinct()
            .ToList();
        if (blockers.Count > 0)
        {
            var sample = string.Join("、", blockers.Take(3));
            var suffix = blockers.Count > 3 ? $" 等 {blockers.Count} 项" : "";
            throw new InvalidOperationException($"无法删除：「{sample}」{suffix}的公式仍引用该项目，请先调整这些公式");
        }

        // 子树删除整体事务化
        await ExecuteInTransactionAsync(async () =>
        {
            foreach (var id in deleteIdSet)
            {
                await _itemRepository.DeleteAsync(id);
            }
        });
        return true;
    }

    public async Task<bool> ReorderItemsAsync(long templateId, List<ReorderAmoebaPLItemRequest> items)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null) return false;

        var existingItems = await _itemRepository.Query()
            .Where(i => i.FTemplateId == templateId)
            .ToListAsync();
        var byId = existingItems.ToDictionary(i => i.FID);

        // 校验整批移动后的结构再落库：父级存在性、防自指、防环、指标分区约束
        var proposedParents = existingItems.ToDictionary(i => i.FID, i => i.FParentId);
        foreach (var req in items)
        {
            if (!byId.TryGetValue(req.ItemId, out var moved)) continue;
            if (req.ParentId == req.ItemId)
                throw new InvalidOperationException($"“{moved.FItemName}”的父级不能是自身");
            if (req.ParentId != 0 && !byId.TryGetValue(req.ParentId, out _))
                throw new InvalidOperationException($"“{moved.FItemName}”的目标父级不存在");
            // 指标分区仅认根级标记（V4 迁移前嵌套分组可能残留误标）
            if (req.ParentId != 0 && byId[req.ParentId].F是否指标分区 && byId[req.ParentId].FParentId == 0 && moved.F项目类别 != "indicator")
                throw new InvalidOperationException($"指标分区下只能放置 indicator 类别的子项（{moved.FItemName}）");
            proposedParents[req.ItemId] = req.ParentId;
        }
        // 防环：仅拒绝本次请求引入的环（沿提议父链上溯能回到被移动节点自身）；
        // 模板中与本次请求无关的存量脏环予以容忍（与删除路径的防环口径一致），避免阻断全部排序操作
        foreach (var req in items)
        {
            if (!byId.ContainsKey(req.ItemId) || req.ParentId == 0) continue;
            var visited = new HashSet<long>();
            var current = req.ParentId;
            while (current != 0)
            {
                if (current == req.ItemId)
                    throw new InvalidOperationException("本次排序调整会形成父子环路，已拒绝保存");
                if (!visited.Add(current)) break; // 存量脏环，终止上溯
                current = proposedParents.TryGetValue(current, out var parent) ? parent : 0;
            }
        }

        foreach (var req in items)
        {
            var item = existingItems.FirstOrDefault(i => i.FID == req.ItemId);
            if (item != null)
            {
                item.FSort = req.Sort;
                item.FParentId = req.ParentId;
                item.FUpdatedTime = DateTime.Now;
                await _itemRepository.UpdateAsync(item);
            }
        }

        return true;
    }

    public async Task<AmoebaPLItemDto> CloneItemFromTemplateAsync(long targetTemplateId, CloneAmoebaPLItemRequest request)
    {
        if (request.SourceTemplateId <= 0)
            throw new ArgumentException("请选择来源模板", nameof(request.SourceTemplateId));
        if (request.SourceItemId <= 0)
            throw new ArgumentException("请选择来源项目", nameof(request.SourceItemId));

        var targetTemplate = await _templateRepository.GetByIdAsync(targetTemplateId);
        if (targetTemplate == null)
            throw new InvalidOperationException("目标模板不存在");

        var sourceItems = await _itemRepository.Query()
            .Where(i => i.FTemplateId == request.SourceTemplateId)
            .OrderBy(i => i.FSort)
            .ThenBy(i => i.FID)
            .ToListAsync();
        var sourceRoot = sourceItems.FirstOrDefault(i => i.FID == request.SourceItemId);
        if (sourceRoot == null)
            throw new InvalidOperationException("来源损益项不存在");

        var targetParentId = request.TargetParentId.GetValueOrDefault();
        if (targetParentId > 0)
        {
            var parentExists = await _itemRepository.Query()
                .AnyAsync(i => i.FTemplateId == targetTemplateId && i.FID == targetParentId);
            if (!parentExists)
                throw new InvalidOperationException("目标父级不存在");
        }
        else if (sourceRoot.FNodeRole != "group" && sourceRoot.FNodeRole != "formula")
        {
            // 根级只渲染板块(group)/全局公式(formula)/指标分区；普通项克隆到根级后
            // 在目标模板任何位置都不可见、不可删，且占用名称唯一性
            throw new InvalidOperationException("普通损益项不能复制到目标模板根级，请指定目标父级分组");
        }

        // 待克隆集合：根 + （可选）全部后代
        var itemsToClone = new List<FinAmoebaPLItem> { sourceRoot };
        if (request.CloneChildren)
        {
            itemsToClone.AddRange(sourceItems
                .Where(i => IsDescendantOf(i.FID, sourceRoot.FID, sourceItems))
                .OrderBy(i => i.FSort)
                .ThenBy(i => i.FID));
        }

        // 名称唯一性预校验（事务前整体校验，避免中途失败）
        foreach (var item in itemsToClone)
        {
            await EnsureItemNameUniqueAsync(targetTemplateId, item.FItemName, excludeItemId: null);
        }

        // 公式引用校验：克隆后的公式引用必须能在（目标模板现有项 ∪ 本次克隆项）中解析，
        // 否则求值静默归 0，用户难以察觉
        var targetNames = (await _itemRepository.Query()
                .Where(i => i.FTemplateId == targetTemplateId)
                .Select(i => i.FItemName)
                .ToListAsync())
            .Concat(itemsToClone.Select(i => i.FItemName))
            .Select(n => (n ?? string.Empty).Trim())
            .ToHashSet();
        foreach (var item in itemsToClone.Where(i => !string.IsNullOrWhiteSpace(i.FFormula)))
        {
            foreach (Match m in Regex.Matches(item.FFormula!, @"\$\{([^}]+)\}"))
            {
                var referenced = m.Groups[1].Value.Trim();
                if (!targetNames.Contains(referenced))
                {
                    throw new InvalidOperationException(
                        $"「{item.FItemName}」的公式引用了目标模板中不存在的项目「{referenced}」，请先在目标模板创建该项目或调整公式");
                }
            }
        }

        var now = DateTime.Now;
        FinAmoebaPLItem clonedRoot = null!;

        // 整体事务化：子树克隆要么全部成功要么全部回滚，不留半个子树
        await ExecuteInTransactionAsync(async () =>
        {
            clonedRoot = await CloneItemNodeAsync(sourceRoot, targetTemplateId, targetParentId, now);

            if (request.CloneChildren)
            {
                var idMap = new Dictionary<long, long> { [sourceRoot.FID] = clonedRoot.FID };
                var pending = itemsToClone.Where(i => i.FID != sourceRoot.FID).ToList();
                var safety = pending.Count + 1;
                while (pending.Count > 0 && safety-- > 0)
                {
                    var ready = pending.Where(i => idMap.ContainsKey(i.FParentId)).ToList();
                    if (ready.Count == 0) break;
                    foreach (var item in ready)
                    {
                        var cloned = await CloneItemNodeAsync(item, targetTemplateId, idMap[item.FParentId], now);
                        idMap[item.FID] = cloned.FID;
                        pending.Remove(item);
                    }
                }
            }
        });

        return MapItemToDto(clonedRoot);
    }

    #endregion


    /// <summary>
    /// 移动目标父级合法性校验：父级须存在于同模板、不能是自身或自身后代（防止形成环路），
    /// 且指标分区下只能放置 indicator 类别的子项。
    /// </summary>
    private async Task EnsureValidParentAsync(long templateId, FinAmoebaPLItem item, long newParentId, string? effectiveCategory)
    {
        if (newParentId == 0) return;
        if (newParentId == item.FID)
        {
            throw new InvalidOperationException("父级不能是自身");
        }

        var allItems = await _itemRepository.Query()
            .Where(i => i.FTemplateId == templateId)
            .ToListAsync();
        var parent = allItems.FirstOrDefault(i => i.FID == newParentId);
        if (parent == null)
        {
            throw new InvalidOperationException("目标父级不存在");
        }

        var descendantIds = new List<long>();
        CollectDescendantIds(item.FID, allItems, descendantIds);
        if (descendantIds.Contains(newParentId))
        {
            throw new InvalidOperationException("不能将损益项移动到其自身的子级下");
        }

        // 指标分区仅认根级标记（V4 迁移前嵌套分组可能残留误标）
        if (parent.F是否指标分区 && parent.FParentId == 0 && effectiveCategory != "indicator")
        {
            throw new InvalidOperationException("指标分区的子项的项目类别必须为 indicator");
        }
    }

    /// <summary>
    /// 同模板下 PLItem 的 FItemName 唯一性校验。
    /// FNodeRole=indicator 的指标项除外（其值通过公式计算不作为引用目标）。
    /// </summary>
    private async Task EnsureItemNameUniqueAsync(long templateId, string itemName, long? excludeItemId)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return;
        var trimmed = itemName.Trim();
        var conflict = await _itemRepository.Query()
            .Where(i => i.FTemplateId == templateId
                        && i.FItemName == trimmed
                        && i.FNodeRole != "indicator"
                        && (excludeItemId == null || i.FID != excludeItemId.Value))
            .AnyAsync();
        if (conflict)
        {
            throw new InvalidOperationException($"同模板下已存在名称为“{trimmed}”的损益项，请使用唯一名称。");
        }
    }

    private static AmoebaPLTemplateDto MapTemplateToDto(FinAmoebaPLTemplate template)
    {
        return new AmoebaPLTemplateDto
        {
            Id = template.FID,
            Name = template.FName,
            Description = template.FDescription,
            IsDefault = template.FIsDefault == 1,
            AccountSetId = template.FAccountSetId,
            Items = BuildItemTree(template.Items)
        };
    }

    private static List<AmoebaPLItemDto> BuildItemTree(List<FinAmoebaPLItem> items)
    {
        var lookup = items.ToLookup(i => i.FParentId);
        
        AmoebaPLItemDto BuildNode(FinAmoebaPLItem item)
        {
            var node = new AmoebaPLItemDto
            {
                Id = item.FID,
                TemplateId = item.FTemplateId,
                ItemName = item.FItemName,
                NodeRole = item.FNodeRole,
                Formula = item.FFormula,
                Sort = item.FSort,
                ParentId = item.FParentId,
                RelatedAccountsJson = item.FRelatedAccountsJson,
                DataSource = item.FDataSource,
                SummaryKeywordsJson = item.FSummaryKeywordsJson,
                Unit = item.FUnit,
                DataSourceRemark = item.FDataSourceRemark,
                CalculationLogic = item.FCalculationLogic,
                PerUnitMode = item.FPerUnitMode,
                DecimalPlaces = item.F小数位数,
                IsManualEntry = item.FIsManualEntry,
                BillingFilterJson = item.FBillingFilterJson,
                // 正交二维字段（新增）
                ItemCategory = item.F项目类别,
                ValueSource = item.F值来源,
                SystemDataSource = item.F系统数据源,
                IsIndicatorSection = item.F是否指标分区,
            };
            
            node.Children = lookup[item.FID].Select(BuildNode).OrderBy(c => c.Sort).ToList();
            return node;
        }
        
        return lookup[0].Select(BuildNode).OrderBy(i => i.Sort).ToList();
    }

    private static AmoebaPLItemDto MapItemToDto(FinAmoebaPLItem item)
    {
        return new AmoebaPLItemDto
        {
            Id = item.FID,
            TemplateId = item.FTemplateId,
            ItemName = item.FItemName,
            NodeRole = item.FNodeRole,
            Formula = item.FFormula,
            Sort = item.FSort,
            ParentId = item.FParentId,
            RelatedAccountsJson = item.FRelatedAccountsJson,
            DataSource = item.FDataSource,
            SummaryKeywordsJson = item.FSummaryKeywordsJson,
            Unit = item.FUnit,
            DataSourceRemark = item.FDataSourceRemark,
            CalculationLogic = item.FCalculationLogic,
            PerUnitMode = item.FPerUnitMode,
            DecimalPlaces = item.F小数位数,
            IsManualEntry = item.FIsManualEntry,
            BillingFilterJson = item.FBillingFilterJson,
            // 正交二维字段（新增）
            ItemCategory = item.F项目类别,
            ValueSource = item.F值来源,
            SystemDataSource = item.F系统数据源,
            IsIndicatorSection = item.F是否指标分区,
        };
    }

    private static void CollectDescendantIds(long parentId, List<FinAmoebaPLItem> allItems, List<long> output)
    {
        CollectDescendantIds(parentId, allItems, output, new HashSet<long> { parentId });
    }

    private static void CollectDescendantIds(long parentId, List<FinAmoebaPLItem> allItems, List<long> output, HashSet<long> visited)
    {
        var children = allItems.Where(i => i.FParentId == parentId).ToList();
        foreach (var child in children)
        {
            if (!visited.Add(child.FID)) continue; // 历史脏数据成环时防止无限递归导致进程崩溃
            CollectDescendantIds(child.FID, allItems, output, visited);
            output.Add(child.FID);
        }
    }

    private static bool IsDescendantOf(long itemId, long ancestorId, List<FinAmoebaPLItem> allItems)
    {
        var itemById = allItems.ToDictionary(i => i.FID);
        var visited = new HashSet<long>();
        var currentId = itemId;
        while (itemById.TryGetValue(currentId, out var current) && current.FParentId != 0)
        {
            if (!visited.Add(currentId)) return false; // 历史脏数据成环时终止
            if (current.FParentId == ancestorId) return true;
            currentId = current.FParentId;
        }
        return false;
    }

    private async Task<FinAmoebaPLItem> CloneItemNodeAsync(FinAmoebaPLItem source, long targetTemplateId, long targetParentId, DateTime now)
    {
        var cloned = new FinAmoebaPLItem
        {
            FTemplateId = targetTemplateId,
            FParentId = targetParentId,
            FItemName = source.FItemName,
            FNodeRole = source.FNodeRole,
            FFormula = source.FFormula,
            FSort = source.FSort,
            FRelatedAccountsJson = source.FRelatedAccountsJson,
            FDataSource = source.FDataSource,
            FSummaryKeywordsJson = source.FSummaryKeywordsJson,
            FBillingFilterJson = source.FBillingFilterJson,
            FUnit = source.FUnit,
            FDataSourceRemark = source.FDataSourceRemark,
            FCalculationLogic = source.FCalculationLogic,
            FPerUnitMode = source.FPerUnitMode,
            F小数位数 = source.F小数位数,
            FIsManualEntry = source.FIsManualEntry,
            F项目类别 = source.F项目类别,
            F值来源 = source.F值来源,
            F系统数据源 = source.F系统数据源,
            F是否指标分区 = source.F是否指标分区,
            FCreatedTime = now,
            FUpdatedTime = now,
        };
        await _itemRepository.AddAsync(cloned);
        return cloned;
    }
}
