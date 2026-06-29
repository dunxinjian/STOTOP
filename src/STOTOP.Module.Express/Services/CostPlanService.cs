using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Models;

namespace STOTOP.Module.Express.Services;

/// <summary>
/// 成本方案服务
/// </summary>
public class CostPlanService : ICostPlanService
{
    private readonly IRepository<ExpCostPlan> _planRepo;
    private readonly IRepository<ExpCostPlanItem> _itemRepo;
    private readonly IRepository<ExpCostPlanItemOutlet> _outletRepo;
    private readonly IRepository<ExpCostPlanItemShop> _shopRepo;
    private readonly IRepository<ExpCostPlanItemPeriod> _periodRepo;
    private readonly IRepository<ExpCostPlanExclusion> _exclusionRepo;
    private readonly IRepository<ExpBrand> _brandRepo;
    private readonly STOTOPDbContext _context;

    public CostPlanService(
        IRepository<ExpCostPlan> planRepo,
        IRepository<ExpCostPlanItem> itemRepo,
        IRepository<ExpCostPlanItemOutlet> outletRepo,
        IRepository<ExpCostPlanItemShop> shopRepo,
        IRepository<ExpCostPlanItemPeriod> periodRepo,
        IRepository<ExpCostPlanExclusion> exclusionRepo,
        IRepository<ExpBrand> brandRepo,
        STOTOPDbContext context)
    {
        _planRepo = planRepo;
        _itemRepo = itemRepo;
        _outletRepo = outletRepo;
        _shopRepo = shopRepo;
        _periodRepo = periodRepo;
        _exclusionRepo = exclusionRepo;
        _brandRepo = brandRepo;
        _context = context;
    }

    /// <summary>
    /// 校验方案归属当前组织（_planRepo.Query 受全局组织过滤器约束）。
    /// Item/Period/Outlet/Shop/Exclusion 子表均无组织字段，所有按 planId 操作子表的方法必须先经此校验，
    /// 防止跨组织读写。
    /// </summary>
    private async Task EnsurePlanInOrgAsync(long planId)
    {
        var exists = await _planRepo.Query().AnyAsync(p => p.FID == planId);
        if (!exists)
            throw new InvalidOperationException("成本方案不存在");
    }

    // ==================== 方案管理 ====================

    public async Task<PagedResult<CostPlanListDto>> GetPlanListAsync(CostPlanQueryRequest request)
    {
        var query = _planRepo.Query();

        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(e => e.FBrandCode == request.BrandCode);
        if (request.Status.HasValue)
            query = query.Where(e => e.FStatus == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(e => e.FPlanName.Contains(keyword));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // 批量查询品牌名称
        var brandCodes = items.Select(e => e.FBrandCode).Distinct().ToList();
        var brands = await _brandRepo.Query()
            .Where(b => brandCodes.Contains(b.FCode))
            .ToDictionaryAsync(b => b.FCode, b => b.FName);

        // 批量查询成本项数量
        var planIds = items.Select(e => e.FID).ToList();
        var itemCounts = await _itemRepo.Query()
            .Where(i => planIds.Contains(i.FPlanId))
            .GroupBy(i => i.FPlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.PlanId, g => g.Count);

        return new PagedResult<CostPlanListDto>
        {
            Items = items.Select(e => new CostPlanListDto
            {
                Id = e.FID,
                BrandCode = e.FBrandCode,
                BrandName = brands.GetValueOrDefault(e.FBrandCode),
                PlanName = e.FPlanName,
                Status = e.FStatus,
                ItemCount = itemCounts.GetValueOrDefault(e.FID),
                CreatedTime = e.FCreatedTime
            }).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<CostPlanDetailDto?> GetPlanByIdAsync(long id)
    {
        var entity = await _planRepo.Query()
            .FirstOrDefaultAsync(e => e.FID == id);

        if (entity == null) return null;

        // 加载成本项 + 网点 + 店铺 + 时间段数量
        var items = await _itemRepo.Query()
            .Where(i => i.FPlanId == id)
            .OrderBy(i => i.FSortOrder)
            .ToListAsync();

        var itemIds = items.Select(i => i.FID).ToList();

        var outlets = await _outletRepo.Query()
            .Where(o => itemIds.Contains(o.FItemId))
            .ToListAsync();

        var shops = await _shopRepo.Query()
            .Where(s => itemIds.Contains(s.FItemId))
            .ToListAsync();

        var periodCounts = await _periodRepo.Query()
            .Where(p => itemIds.Contains(p.FItemId))
            .GroupBy(p => p.FItemId)
            .Select(g => new { ItemId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ItemId, g => g.Count);

        // 加载互斥配置
        var exclusions = await _exclusionRepo.Query()
            .Where(ex => ex.FPlanId == id)
            .OrderBy(ex => ex.FEffectiveDate)
            .ToListAsync();

        return new CostPlanDetailDto
        {
            Id = entity.FID,
            BrandCode = entity.FBrandCode,
            PlanName = entity.FPlanName,
            Status = entity.FStatus,
            OrgId = entity.FOrgId,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime,
            Items = items.Select(i => new CostPlanItemDto
            {
                Id = i.FID,
                PlanId = i.FPlanId,
                ItemName = i.FItemName,
                ItemType = i.FItemType,
                SettlementWeightStage = i.FSettlementWeightStage,
                SortOrder = i.FSortOrder,
                OutletIds = outlets.Where(o => o.FItemId == i.FID).Select(o => o.FOutletId).ToList(),
                ShopNames = shops.Where(s => s.FItemId == i.FID).Select(s => s.FShopName).ToList(),
                PeriodCount = periodCounts.GetValueOrDefault(i.FID)
            }).ToList(),
            Exclusions = exclusions.Select(ex => new CostPlanExclusionDto
            {
                Id = ex.FID,
                PlanId = ex.FPlanId,
                EffectiveDate = ex.FEffectiveDate,
                ExclusionRuleJson = ex.FExclusionRuleJson,
                CreatedTime = ex.FCreatedTime,
                UpdatedTime = ex.FUpdatedTime
            }).ToList()
        };
    }

    public async Task<CostPlanDetailDto> CreatePlanAsync(CreatePlanRequest request)
    {
        var entity = new ExpCostPlan
        {
            FBrandCode = request.BrandCode,
            FPlanName = request.PlanName,
            FStatus = 0, // 草稿
            FOrgId = 0,  // 由 IOrgScoped 过滤器自动填充
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now,
        };

        var result = await _planRepo.AddAsync(entity);

        return (await GetPlanByIdAsync(result.FID))!;
    }

    public async Task<CostPlanDetailDto?> UpdatePlanAsync(long id, UpdatePlanRequest request)
    {
        var entity = await _planRepo.Query()
            .AsTracking()
            .FirstOrDefaultAsync(e => e.FID == id);

        if (entity == null) return null;

        // 启用中的方案不允许改品牌，否则会绕过"同组织同品牌仅一个启用方案"约束造成双启用
        if (entity.FStatus == 1 && entity.FBrandCode != request.BrandCode)
            throw new InvalidOperationException("启用中的方案不可修改品牌，请先停用");

        entity.FBrandCode = request.BrandCode;
        entity.FPlanName = request.PlanName;
        entity.FUpdatedTime = DateTime.Now;
        await _planRepo.UpdateAsync(entity);

        return (await GetPlanByIdAsync(id))!;
    }

    public async Task<bool> DeletePlanAsync(long id)
    {
        var entity = await _planRepo.Query()
            .FirstOrDefaultAsync(e => e.FID == id);

        if (entity == null) return false;

        if (entity.FStatus == 1)
            throw new InvalidOperationException("启用中的方案不可删除，请先停用");

        // 级联删除子表（事务内原子完成，避免删一半失败留下不一致数据）
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var itemIds = await _itemRepo.Query()
                .Where(i => i.FPlanId == id)
                .Select(i => i.FID)
                .ToListAsync();

            foreach (var itemId in itemIds)
            {
                // 删除时间段
                var periodIds = await _periodRepo.Query()
                    .Where(p => p.FItemId == itemId)
                    .Select(p => p.FID)
                    .ToListAsync();
                foreach (var pid in periodIds)
                    await _periodRepo.DeleteAsync(pid);

                // 删除网点
                var outletIds = await _outletRepo.Query()
                    .Where(o => o.FItemId == itemId)
                    .Select(o => o.FID)
                    .ToListAsync();
                foreach (var oid in outletIds)
                    await _outletRepo.DeleteAsync(oid);

                // 删除店铺
                var shopIds = await _shopRepo.Query()
                    .Where(s => s.FItemId == itemId)
                    .Select(s => s.FID)
                    .ToListAsync();
                foreach (var sid in shopIds)
                    await _shopRepo.DeleteAsync(sid);

                // 删除成本项
                await _itemRepo.DeleteAsync(itemId);
            }

            // 删除互斥配置
            var exclusionIds = await _exclusionRepo.Query()
                .Where(ex => ex.FPlanId == id)
                .Select(ex => ex.FID)
                .ToListAsync();
            foreach (var eid in exclusionIds)
                await _exclusionRepo.DeleteAsync(eid);

            // 删除主表
            await _planRepo.DeleteAsync(id);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        return true;
    }

    public async Task<bool> ActivatePlanAsync(long id)
    {
        // 事务（可串行化）保护"检查无其他启用方案→启用"两步，防止并发双启用
        await using var tx = await _context.Database.BeginTransactionAsync(global::System.Data.IsolationLevel.Serializable);
        try
        {
            var entity = await _planRepo.Query()
                .FirstOrDefaultAsync(e => e.FID == id);
            if (entity == null) return false;
            // 草稿(0)和已停用(2)均可启用，停用的方案可重新启用
            if (entity.FStatus != 0 && entity.FStatus != 2)
                throw new InvalidOperationException("只有草稿或已停用状态的方案可以启用");

            // 校验同组织+同品牌无其他启用方案
            var hasActive = await _planRepo.Query()
                .AnyAsync(p => p.FOrgId == entity.FOrgId
                            && p.FBrandCode == entity.FBrandCode
                            && p.FStatus == 1
                            && p.FID != id);
            if (hasActive)
                throw new InvalidOperationException($"该组织下品牌 {entity.FBrandCode} 已有启用方案，请先停用");

            // 启用当前方案
            entity.FStatus = 1;
            entity.FUpdatedTime = DateTime.Now;
            await _planRepo.UpdateAsync(entity);
            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeactivatePlanAsync(long id)
    {
        var entity = await _planRepo.Query()
            .FirstOrDefaultAsync(e => e.FID == id);
        if (entity == null) return false;
        if (entity.FStatus != 1)
            throw new InvalidOperationException("只有启用中的方案可以停用");

        entity.FStatus = 2;
        entity.FUpdatedTime = DateTime.Now;
        await _planRepo.UpdateAsync(entity);
        return true;
    }

    // ==================== 成本项管理 ====================

    public async Task<List<CostPlanItemDto>> GetItemsAsync(long planId)
    {
        await EnsurePlanInOrgAsync(planId);

        var items = await _itemRepo.Query()
            .Where(i => i.FPlanId == planId)
            .OrderBy(i => i.FSortOrder)
            .ToListAsync();

        var itemIds = items.Select(i => i.FID).ToList();

        var outlets = await _outletRepo.Query()
            .Where(o => itemIds.Contains(o.FItemId))
            .ToListAsync();

        var shops = await _shopRepo.Query()
            .Where(s => itemIds.Contains(s.FItemId))
            .ToListAsync();

        var periodCounts = await _periodRepo.Query()
            .Where(p => itemIds.Contains(p.FItemId))
            .GroupBy(p => p.FItemId)
            .Select(g => new { ItemId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ItemId, g => g.Count);

        return items.Select(i => new CostPlanItemDto
        {
            Id = i.FID,
            PlanId = i.FPlanId,
            ItemName = i.FItemName,
            ItemType = i.FItemType,
            SettlementWeightStage = i.FSettlementWeightStage,
            SortOrder = i.FSortOrder,
            OutletIds = outlets.Where(o => o.FItemId == i.FID).Select(o => o.FOutletId).ToList(),
            ShopNames = shops.Where(s => s.FItemId == i.FID).Select(s => s.FShopName).ToList(),
            PeriodCount = periodCounts.GetValueOrDefault(i.FID)
        }).ToList();
    }

    public async Task<CostPlanItemDto> CreateItemAsync(long planId, CreateItemRequest request)
    {
        var plan = await _planRepo.Query().FirstOrDefaultAsync(p => p.FID == planId);
        if (plan == null)
            throw new InvalidOperationException("成本方案不存在");

        var entity = new ExpCostPlanItem
        {
            FPlanId = planId,
            FItemName = request.ItemName,
            FItemType = request.ItemType,
            FSettlementWeightStage = request.SettlementWeightStage,
            FSortOrder = request.SortOrder,
        };

        var result = await _itemRepo.AddAsync(entity);

        return new CostPlanItemDto
        {
            Id = result.FID,
            PlanId = planId,
            ItemName = result.FItemName,
            ItemType = result.FItemType,
            SettlementWeightStage = result.FSettlementWeightStage,
            SortOrder = result.FSortOrder,
            OutletIds = new List<long>(),
            ShopNames = new List<string>(),
            PeriodCount = 0
        };
    }

    public async Task<CostPlanItemDto?> UpdateItemAsync(long planId, long itemId, UpdateItemRequest request)
    {
        await EnsurePlanInOrgAsync(planId);

        var entity = await _itemRepo.Query()
            .FirstOrDefaultAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (entity == null) return null;

        var oldItemType = entity.FItemType;

        entity.FItemName = request.ItemName;
        entity.FItemType = request.ItemType;
        entity.FSettlementWeightStage = request.SettlementWeightStage;
        entity.FSortOrder = request.SortOrder;
        await _itemRepo.UpdateAsync(entity);

        // 类型从一口价(4)改为其他类型时，清理仅一口价使用的店铺关联，避免残留维度数据
        if (oldItemType == 4 && request.ItemType != 4)
        {
            var staleShops = await _shopRepo.Query()
                .Where(s => s.FItemId == itemId)
                .ToListAsync();
            foreach (var s in staleShops)
                await _shopRepo.DeleteAsync(s.FID);
        }

        // 重新查询完整数据
        var items = await GetItemsAsync(planId);
        return items.FirstOrDefault(i => i.Id == itemId);
    }

    public async Task<bool> DeleteItemAsync(long planId, long itemId)
    {
        await EnsurePlanInOrgAsync(planId);

        var entity = await _itemRepo.Query()
            .FirstOrDefaultAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (entity == null) return false;

        // 校验该项是否被互斥配置JSON引用
        var exclusions = await _exclusionRepo.Query()
            .Where(ex => ex.FPlanId == planId && ex.FExclusionRuleJson != null)
            .ToListAsync();

        foreach (var ex in exclusions)
        {
            try
            {
                using var doc = JsonDocument.Parse(ex.FExclusionRuleJson!);
                var root = doc.RootElement;
                if (root.TryGetProperty("excludedCostItemIds", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in arr.EnumerateArray())
                    {
                        if (elem.ValueKind == JsonValueKind.Number
                            && elem.TryGetInt64(out var excludedId)
                            && excludedId == itemId)
                            throw new InvalidOperationException(
                                $"成本项被互斥配置（生效日期 {ex.FEffectiveDate:yyyy-MM-dd}）引用，无法删除");
                    }
                }
            }
            catch (JsonException)
            {
                // JSON 解析失败时忽略，不阻塞删除
            }
        }

        // 级联删除子表（事务内原子完成，避免删一半失败留下不一致数据）
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var periodIds = await _periodRepo.Query()
                .Where(p => p.FItemId == itemId)
                .Select(p => p.FID)
                .ToListAsync();
            foreach (var pid in periodIds)
                await _periodRepo.DeleteAsync(pid);

            var outletIds = await _outletRepo.Query()
                .Where(o => o.FItemId == itemId)
                .Select(o => o.FID)
                .ToListAsync();
            foreach (var oid in outletIds)
                await _outletRepo.DeleteAsync(oid);

            var shopIds = await _shopRepo.Query()
                .Where(s => s.FItemId == itemId)
                .Select(s => s.FID)
                .ToListAsync();
            foreach (var sid in shopIds)
                await _shopRepo.DeleteAsync(sid);

            await _itemRepo.DeleteAsync(itemId);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        return true;
    }

    // ==================== 应用网点管理 ====================

    public async Task<List<long>> GetItemOutletsAsync(long planId, long itemId)
    {
        await EnsurePlanInOrgAsync(planId);

        // 校验成本项归属该方案，防止任意 itemId 跨方案/跨组织读取
        var itemBelongs = await _itemRepo.Query()
            .AnyAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (!itemBelongs) return new List<long>();

        return await _outletRepo.Query()
            .Where(o => o.FItemId == itemId)
            .Select(o => o.FOutletId)
            .ToListAsync();
    }

    public async Task SetItemOutletsAsync(long planId, long itemId, List<long> outletIds)
    {
        var item = await _itemRepo.Query()
            .FirstOrDefaultAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (item == null)
            throw new InvalidOperationException("成本项不存在");

        var plan = await _planRepo.Query().FirstOrDefaultAsync(p => p.FID == planId);
        if (plan == null)
            throw new InvalidOperationException("成本方案不存在");

        // 仅对启用方案做应用层校验
        if (plan.FStatus == 1 && outletIds.Count > 0)
        {
            // 先找出同品牌+同组织+启用状态的其他方案ID
            var otherActivePlanIds = await _planRepo.Query()
                .Where(p => p.FID != planId
                         && p.FBrandCode == plan.FBrandCode
                         && p.FOrgId == plan.FOrgId
                         && p.FStatus == 1)
                .Select(p => p.FID)
                .ToListAsync();

            if (item.FItemType != 4)
            {
                // 非一口价成本项：检查同品牌下这些网点是否已被其他启用方案的同类型成本项覆盖
                var conflictingItems = await _itemRepo.Query()
                    .Where(i => otherActivePlanIds.Contains(i.FPlanId)
                             && i.FItemType == item.FItemType)
                    .Select(i => i.FID)
                    .ToListAsync();

                if (conflictingItems.Count > 0)
                {
                    var conflictingOutlets = await _outletRepo.Query()
                        .Where(o => conflictingItems.Contains(o.FItemId) && outletIds.Contains(o.FOutletId))
                        .Select(o => o.FOutletId)
                        .Distinct()
                        .ToListAsync();

                    if (conflictingOutlets.Count > 0)
                        throw new InvalidOperationException(
                            $"以下网点已被其他启用方案的同类型成本项覆盖：{string.Join(", ", conflictingOutlets)}");
                }
            }
            else if (item.FItemType == 4)
            {
                // 一口价类型：同网点允许多个（通过店铺区分），但需校验关联店铺不重叠
                var sameBrandFixedItems = await _itemRepo.Query()
                    .Where(i => otherActivePlanIds.Contains(i.FPlanId)
                             && i.FItemType == 4)
                    .Select(i => i.FID)
                    .ToListAsync();

                if (sameBrandFixedItems.Count > 0)
                {
                    var overlappingOutlets = await _outletRepo.Query()
                        .Where(o => sameBrandFixedItems.Contains(o.FItemId) && outletIds.Contains(o.FOutletId))
                        .Select(o => new { o.FItemId, o.FOutletId })
                        .ToListAsync();

                    if (overlappingOutlets.Count > 0)
                    {
                        // 有网点重叠，需检查店铺是否也重叠
                        var overlappingItemIds = overlappingOutlets.Select(o => o.FItemId).Distinct().ToList();
                        var existingShops = await _shopRepo.Query()
                            .Where(s => overlappingItemIds.Contains(s.FItemId))
                            .Select(s => s.FShopName)
                            .ToListAsync();

                        // 当前项的店铺
                        var currentShops = await _shopRepo.Query()
                            .Where(s => s.FItemId == itemId)
                            .Select(s => s.FShopName)
                            .ToListAsync();

                        var shopOverlap = existingShops.Intersect(currentShops, StringComparer.OrdinalIgnoreCase).ToList();
                        if (shopOverlap.Count > 0)
                            throw new InvalidOperationException(
                                $"一口价成本项的关联店铺存在重叠：{string.Join(", ", shopOverlap)}");
                    }
                }
            }
        }

        // 全量替换（事务内原子完成：删一半失败会使网点配置变空，而"空网点=全部网点适用"会错误扩大计费范围）
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var existing = await _outletRepo.Query()
                .Where(o => o.FItemId == itemId)
                .ToListAsync();
            foreach (var o in existing)
                await _outletRepo.DeleteAsync(o.FID);

            foreach (var outletId in outletIds.Distinct())
            {
                await _outletRepo.AddAsync(new ExpCostPlanItemOutlet
                {
                    FItemId = itemId,
                    FOutletId = outletId
                });
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ==================== 关联店铺管理（一口价专用）====================

    public async Task<List<string>> GetItemShopsAsync(long planId, long itemId)
    {
        await EnsurePlanInOrgAsync(planId);

        var itemBelongs = await _itemRepo.Query()
            .AnyAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (!itemBelongs) return new List<string>();

        return await _shopRepo.Query()
            .Where(s => s.FItemId == itemId)
            .Select(s => s.FShopName)
            .ToListAsync();
    }

    public async Task SetItemShopsAsync(long planId, long itemId, List<string> shopNames)
    {
        var item = await _itemRepo.Query()
            .FirstOrDefaultAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (item == null)
            throw new InvalidOperationException("成本项不存在");

        var plan = await _planRepo.Query().FirstOrDefaultAsync(p => p.FID == planId);
        if (plan == null)
            throw new InvalidOperationException("成本方案不存在");

        // 校验同网点下多个一口价成本项的关联店铺不重叠
        if (plan.FStatus == 1 && shopNames.Count > 0 && item.FItemType == 4)
        {
            // 获取当前项的网点
            var currentItemOutlets = await _outletRepo.Query()
                .Where(o => o.FItemId == itemId)
                .Select(o => o.FOutletId)
                .ToListAsync();

            if (currentItemOutlets.Count > 0)
            {
                // 查找同方案、同网点下的其他一口价成本项
                var otherItemIds = await _itemRepo.Query()
                    .Where(i => i.FPlanId == planId && i.FID != itemId && i.FItemType == 4)
                    .Select(i => i.FID)
                    .ToListAsync();

                if (otherItemIds.Count > 0)
                {
                    var otherOutlets = await _outletRepo.Query()
                        .Where(o => otherItemIds.Contains(o.FItemId) && currentItemOutlets.Contains(o.FOutletId))
                        .Select(o => o.FItemId)
                        .Distinct()
                        .ToListAsync();

                    if (otherOutlets.Count > 0)
                    {
                        var otherShops = await _shopRepo.Query()
                            .Where(s => otherOutlets.Contains(s.FItemId))
                            .Select(s => s.FShopName)
                            .ToListAsync();

                        var shopOverlap = otherShops.Intersect(shopNames, StringComparer.OrdinalIgnoreCase).ToList();
                        if (shopOverlap.Count > 0)
                            throw new InvalidOperationException(
                                $"同网点下其他一口价成本项已关联以下店铺：{string.Join(", ", shopOverlap)}");
                    }
                }
            }
        }

        // 全量替换（事务内原子完成）
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var existing = await _shopRepo.Query()
                .Where(s => s.FItemId == itemId)
                .ToListAsync();
            foreach (var s in existing)
                await _shopRepo.DeleteAsync(s.FID);

            foreach (var name in shopNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await _shopRepo.AddAsync(new ExpCostPlanItemShop
                {
                    FItemId = itemId,
                    FShopName = name
                });
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ==================== 时间段管理 ====================

    public async Task<List<CostPlanItemPeriodDto>> GetPeriodsAsync(long planId, long itemId)
    {
        await EnsurePlanInOrgAsync(planId);

        var item = await _itemRepo.Query()
            .FirstOrDefaultAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (item == null) return new List<CostPlanItemPeriodDto>();

        return await _periodRepo.Query()
            .Where(p => p.FItemId == itemId)
            .OrderBy(p => p.FEffectiveDate)
            .Select(p => new CostPlanItemPeriodDto
            {
                Id = p.FID,
                ItemId = p.FItemId,
                EffectiveDate = p.FEffectiveDate,
                MatrixJson = p.FMatrixJson,
                CreatedTime = p.FCreatedTime,
                UpdatedTime = p.FUpdatedTime
            }).ToListAsync();
    }

    public async Task<CostPlanItemPeriodDto> CreatePeriodAsync(long planId, long itemId, CreatePeriodRequest request)
    {
        await EnsurePlanInOrgAsync(planId);

        var item = await _itemRepo.Query()
            .FirstOrDefaultAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (item == null)
            throw new InvalidOperationException("成本项不存在");

        // 归一化到日期（去时间分量），防止同一天因时分秒不同建出多个期间、后续精确匹配错失
        var effectiveDate = request.EffectiveDate.Date;

        // 校验同一成本项下生效日期不可重复
        var exists = await _periodRepo.Query()
            .AnyAsync(p => p.FItemId == itemId && p.FEffectiveDate == effectiveDate);
        if (exists)
            throw new InvalidOperationException($"生效日期 {effectiveDate:yyyy-MM-dd} 已存在");

        var entity = new ExpCostPlanItemPeriod
        {
            FItemId = itemId,
            FEffectiveDate = effectiveDate,
            FMatrixJson = request.MatrixJson,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now,
        };

        var result = await _periodRepo.AddAsync(entity);

        return new CostPlanItemPeriodDto
        {
            Id = result.FID,
            ItemId = result.FItemId,
            EffectiveDate = result.FEffectiveDate,
            MatrixJson = result.FMatrixJson,
            CreatedTime = result.FCreatedTime,
            UpdatedTime = result.FUpdatedTime
        };
    }

    public async Task<CostPlanItemPeriodDto?> UpdatePeriodAsync(long planId, long itemId, long periodId, UpdatePeriodRequest request)
    {
        await EnsurePlanInOrgAsync(planId);

        var itemBelongs = await _itemRepo.Query()
            .AnyAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (!itemBelongs) return null;

        var entity = await _periodRepo.Query()
            .FirstOrDefaultAsync(p => p.FID == periodId && p.FItemId == itemId);
        if (entity == null) return null;

        entity.FMatrixJson = request.MatrixJson;
        entity.FUpdatedTime = DateTime.Now;
        await _periodRepo.UpdateAsync(entity);

        return new CostPlanItemPeriodDto
        {
            Id = entity.FID,
            ItemId = entity.FItemId,
            EffectiveDate = entity.FEffectiveDate,
            MatrixJson = entity.FMatrixJson,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    public async Task<bool> DeletePeriodAsync(long planId, long itemId, long periodId)
    {
        await EnsurePlanInOrgAsync(planId);

        var itemBelongs = await _itemRepo.Query()
            .AnyAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (!itemBelongs) return false;

        var entity = await _periodRepo.Query()
            .FirstOrDefaultAsync(p => p.FID == periodId && p.FItemId == itemId);
        if (entity == null) return false;

        await _periodRepo.DeleteAsync(periodId);
        return true;
    }

    // ==================== 互斥配置管理 ====================

    /// <summary>
    /// 校验互斥规则 JSON：必须是合法 JSON 对象，excludedCostItemIds（若有）必须是数字数组。
    /// 写入侧不校验会导致删除保护与计费互斥规则被坏数据静默击穿。
    /// </summary>
    private static void ValidateExclusionRuleJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"互斥规则 JSON 格式无效：{ex.Message}");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("互斥规则 JSON 必须是对象");

            if (doc.RootElement.TryGetProperty("excludedCostItemIds", out var arr))
            {
                if (arr.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("excludedCostItemIds 必须是数组");
                foreach (var elem in arr.EnumerateArray())
                {
                    if (elem.ValueKind != JsonValueKind.Number || !elem.TryGetInt64(out _))
                        throw new InvalidOperationException("excludedCostItemIds 数组元素必须是整数");
                }
            }
        }
    }

    public async Task<List<CostPlanExclusionDto>> GetExclusionsAsync(long planId)
    {
        await EnsurePlanInOrgAsync(planId);

        return await _exclusionRepo.Query()
            .Where(ex => ex.FPlanId == planId)
            .OrderBy(ex => ex.FEffectiveDate)
            .Select(ex => new CostPlanExclusionDto
            {
                Id = ex.FID,
                PlanId = ex.FPlanId,
                EffectiveDate = ex.FEffectiveDate,
                ExclusionRuleJson = ex.FExclusionRuleJson,
                CreatedTime = ex.FCreatedTime,
                UpdatedTime = ex.FUpdatedTime
            }).ToListAsync();
    }

    public async Task<CostPlanExclusionDto> CreateExclusionAsync(long planId, CreateExclusionRequest request)
    {
        var plan = await _planRepo.Query().FirstOrDefaultAsync(p => p.FID == planId);
        if (plan == null)
            throw new InvalidOperationException("成本方案不存在");

        ValidateExclusionRuleJson(request.ExclusionRuleJson);

        // 归一化到日期，防止时间分量绕过同日唯一性
        var effectiveDate = request.EffectiveDate.Date;

        // 校验同方案下生效日期不重复
        var exists = await _exclusionRepo.Query()
            .AnyAsync(ex => ex.FPlanId == planId && ex.FEffectiveDate == effectiveDate);
        if (exists)
            throw new InvalidOperationException($"生效日期 {effectiveDate:yyyy-MM-dd} 的互斥配置已存在");

        var entity = new ExpCostPlanExclusion
        {
            FPlanId = planId,
            FEffectiveDate = effectiveDate,
            FExclusionRuleJson = request.ExclusionRuleJson,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now,
        };

        var result = await _exclusionRepo.AddAsync(entity);

        return new CostPlanExclusionDto
        {
            Id = result.FID,
            PlanId = result.FPlanId,
            EffectiveDate = result.FEffectiveDate,
            ExclusionRuleJson = result.FExclusionRuleJson,
            CreatedTime = result.FCreatedTime,
            UpdatedTime = result.FUpdatedTime
        };
    }

    public async Task<CostPlanExclusionDto?> UpdateExclusionAsync(long planId, long exclusionId, UpdateExclusionRequest request)
    {
        await EnsurePlanInOrgAsync(planId);

        var entity = await _exclusionRepo.Query()
            .FirstOrDefaultAsync(ex => ex.FID == exclusionId && ex.FPlanId == planId);
        if (entity == null) return null;

        ValidateExclusionRuleJson(request.ExclusionRuleJson);

        entity.FExclusionRuleJson = request.ExclusionRuleJson;
        entity.FUpdatedTime = DateTime.Now;
        await _exclusionRepo.UpdateAsync(entity);

        return new CostPlanExclusionDto
        {
            Id = entity.FID,
            PlanId = entity.FPlanId,
            EffectiveDate = entity.FEffectiveDate,
            ExclusionRuleJson = entity.FExclusionRuleJson,
            CreatedTime = entity.FCreatedTime,
            UpdatedTime = entity.FUpdatedTime
        };
    }

    public async Task<bool> DeleteExclusionAsync(long planId, long exclusionId)
    {
        await EnsurePlanInOrgAsync(planId);

        var entity = await _exclusionRepo.Query()
            .FirstOrDefaultAsync(ex => ex.FID == exclusionId && ex.FPlanId == planId);
        if (entity == null) return false;

        await _exclusionRepo.DeleteAsync(exclusionId);
        return true;
    }

    // ==================== 运单成本计算 ====================

    public async Task<EffectiveCostResult?> GetEffectiveCostAsync(string brandCode, long outletId, string? shopName, DateTime businessDate)
    {
        // 1. 找到该品牌的启用成本方案
        var plan = await _planRepo.Query()
            .FirstOrDefaultAsync(p => p.FBrandCode == brandCode && p.FStatus == 1);
        if (plan == null) return null;

        // 2. 加载方案下所有成本项
        var items = await _itemRepo.Query()
            .Where(i => i.FPlanId == plan.FID)
            .OrderBy(i => i.FSortOrder)
            .ToListAsync();

        var itemIds = items.Select(i => i.FID).ToList();

        // 3. 加载应用网点
        var outlets = await _outletRepo.Query()
            .Where(o => itemIds.Contains(o.FItemId))
            .ToListAsync();

        // 筛选出应用网点包含 outletId 的成本项
        var matchedItemIds = outlets
            .Where(o => o.FOutletId == outletId)
            .Select(o => o.FItemId)
            .ToHashSet();

        var matchedItems = items.Where(i => matchedItemIds.Contains(i.FID)).ToList();

        // 4. 查找互斥配置
        var exclusion = await _exclusionRepo.Query()
            .Where(ex => ex.FPlanId == plan.FID && ex.FEffectiveDate <= businessDate)
            .OrderByDescending(ex => ex.FEffectiveDate)
            .FirstOrDefaultAsync();

        // 解析互斥规则
        HashSet<long> excludedItemIds = new();
        if (exclusion?.FExclusionRuleJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(exclusion.FExclusionRuleJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("excludedCostItemIds", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in arr.EnumerateArray())
                    {
                        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt64(out var excludedId))
                            excludedItemIds.Add(excludedId);
                    }
                }
            }
            catch (JsonException) { /* ignore */ }
        }

        // 加载店铺关联
        var shops = await _shopRepo.Query()
            .Where(s => itemIds.Contains(s.FItemId))
            .ToListAsync();

        // 加载时间段
        var periods = await _periodRepo.Query()
            .Where(p => itemIds.Contains(p.FItemId) && p.FEffectiveDate <= businessDate)
            .ToListAsync();

        var result = new EffectiveCostResult();
        var breakdowns = new List<CostBreakdownItem>();
        string mode = "standard";

        // 5. 处理一口价成本项（ItemType=4）
        var fixedPriceItems = matchedItems.Where(i => i.FItemType == 4).ToList();
        foreach (var item in fixedPriceItems)
        {
            // 检查关联店铺是否包含 shopName
            var itemShops = shops.Where(s => s.FItemId == item.FID).Select(s => s.FShopName).ToList();
            bool shopMatched = string.IsNullOrEmpty(shopName)
                ? itemShops.Count == 0  // 无店铺限制时匹配
                : itemShops.Any(s => string.Equals(s, shopName, StringComparison.OrdinalIgnoreCase));

            if (shopMatched)
            {
                mode = "fixed_price";
                // 按时间链匹配矩阵（暂简化为返回 MatrixJson，具体解析后续处理）
                var period = periods
                    .Where(p => p.FItemId == item.FID)
                    .OrderByDescending(p => p.FEffectiveDate)
                    .FirstOrDefault();

                breakdowns.Add(new CostBreakdownItem
                {
                    ItemId = item.FID,
                    ItemName = item.FItemName,
                    ItemType = item.FItemType,
                    Amount = 0 // TODO: 解析 MatrixJson 计算一口价金额
                });

                break; // 一口价只匹配第一个
            }
        }

        // 6. 处理非一口价成本项（ItemType=1/2/3）。
        //    互斥规则是"一口价与其他项的互斥关系"：仅一口价命中时才排除互斥项，
        //    未命中一口价时不应误删普通成本项（否则静默少算成本）。
        var effectiveExcludedIds = mode == "fixed_price" ? excludedItemIds : new HashSet<long>();
        var otherItems = matchedItems.Where(i => i.FItemType != 4 && !effectiveExcludedIds.Contains(i.FID)).ToList();
        foreach (var item in otherItems)
        {
            var period = periods
                .Where(p => p.FItemId == item.FID)
                .OrderByDescending(p => p.FEffectiveDate)
                .FirstOrDefault();

            breakdowns.Add(new CostBreakdownItem
            {
                ItemId = item.FID,
                ItemName = item.FItemName,
                ItemType = item.FItemType,
                Amount = 0 // TODO: 解析 MatrixJson 计算金额
            });
        }

        result.Mode = mode;
        result.Breakdowns = breakdowns;
        result.TotalCost = breakdowns.Sum(b => b.Amount);

        return result;
    }

    // ==================== 矩阵保存/读取 ====================

    public async Task SaveItemMatrixAsync(long planId, long itemId, SaveItemMatrixRequest request)
    {
        await EnsurePlanInOrgAsync(planId);

        var item = await _itemRepo.Query()
            .FirstOrDefaultAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (item == null)
            throw new InvalidOperationException("成本项不存在");

        // 验证 pricingScope 合法性
        var validScopes = new[] { "national", "province", "city" };
        if (!validScopes.Contains(request.PricingScope))
            throw new InvalidOperationException($"无效的定价范围：{request.PricingScope}，应为 national/province/city");

        // 城市加收：单元格缺省份ID时按城市ID回填（旧版前端不携带 provinceId，
        // 计费引擎 FindCellCity 按 (省份ID, 城市名) 匹配，缺省份的城市行永远不命中）
        if (request.PricingScope == "city")
        {
            var missingCityIds = request.Segments
                .SelectMany(s => s.Cells)
                .Where(c => c.CityId is > 0 && (c.ProvinceId is null or 0))
                .Select(c => c.CityId!.Value)
                .Distinct()
                .ToList();
            if (missingCityIds.Count > 0)
            {
                var cityProvinceMap = await _context.Set<ExpCity>()
                    .Where(c => missingCityIds.Contains(c.FID))
                    .ToDictionaryAsync(c => c.FID, c => c.FProvinceId);
                foreach (var cell in request.Segments.SelectMany(s => s.Cells))
                {
                    if (cell.CityId is > 0 && (cell.ProvinceId is null or 0)
                        && cityProvinceMap.TryGetValue(cell.CityId.Value, out var provinceId)
                        && provinceId > 0)
                        cell.ProvinceId = provinceId;
                }
            }
        }

        // 验证 pricingScope 与 cells 数据一致性
        ValidatePricingScopeCells(request.PricingScope, request.Segments);

        // 将请求 DTO 转换为 CostItemEntry 模型
        var entry = new CostItemEntry
        {
            CostItemId = request.CostItemId,
            PricingScope = request.PricingScope,
            Segments = request.Segments.Select(s => new PricingSegment
            {
                CalcMethod = s.CalcMethod,
                SegmentIndex = s.SegmentIndex,
                WeightFrom = s.WeightFrom,
                WeightTo = s.WeightTo,
                RoundingMethod = s.RoundingMethod,
                TruncParam = s.TruncParam,
                CeilParam = s.CeilParam,
                Cells = s.Cells.Select(c => new PricingCell
                {
                    ProvinceId = c.ProvinceId ?? 0,
                    CityId = c.CityId,
                    CityName = c.CityName,
                    BasePrice = c.BasePrice,
                    ContinuePrice = c.ContinuePrice,
                    FirstWeight = c.FirstWeight,
                    ContinueStep = c.ContinueStep == 0 ? 1 : c.ContinueStep,
                    RoundingMethodOverride = c.RoundingMethodOverride,
                    TruncParamOverride = c.TruncParamOverride,
                    CeilParamOverride = c.CeilParamOverride,
                }).ToList()
            }).ToList()
        };

        // 查找匹配的时间段：
        //   指定了生效日期 → 精确匹配（按日归一化），匹配不到则为该日期新建期间，
        //     绝不回退覆盖其他期间（否则会把新价静默写进当前生效期间，污染历史价格）；
        //   未指定生效日期 → 取最新期间，无任何期间时创建默认期间。
        var periodQuery = _periodRepo.Query().Where(p => p.FItemId == itemId);
        ExpCostPlanItemPeriod? period;

        if (!string.IsNullOrWhiteSpace(request.EffectiveDate))
        {
            if (!DateTime.TryParse(request.EffectiveDate, out var ed))
                throw new InvalidOperationException($"无效的生效日期：{request.EffectiveDate}");

            var effectiveDate = ed.Date;
            period = await periodQuery
                .Where(p => p.FEffectiveDate == effectiveDate)
                .FirstOrDefaultAsync();

            period ??= await _periodRepo.AddAsync(new ExpCostPlanItemPeriod
            {
                FItemId = itemId,
                FEffectiveDate = effectiveDate,
                FMatrixJson = string.Empty,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now,
            });
        }
        else
        {
            period = await periodQuery
                .OrderByDescending(p => p.FEffectiveDate)
                .FirstOrDefaultAsync();

            period ??= await _periodRepo.AddAsync(new ExpCostPlanItemPeriod
            {
                FItemId = itemId,
                FEffectiveDate = new DateTime(2020, 1, 1), // 默认生效日期
                FMatrixJson = string.Empty,
                FCreatedTime = DateTime.Now,
                FUpdatedTime = DateTime.Now,
            });
        }

        // 解析现有矩阵 JSON（支持多成本项）
        var matrix = PricingMatrixSerializer.DeserializeCostPlan(period.FMatrixJson);

        // 更新或添加当前成本项的矩阵数据
        var existingIndex = matrix.CostItems.FindIndex(ci => ci.CostItemId == request.CostItemId);
        if (existingIndex >= 0)
            matrix.CostItems[existingIndex] = entry;
        else
            matrix.CostItems.Add(entry);

        // 序列化并保存
        period.FMatrixJson = PricingMatrixSerializer.SerializeCostPlan(matrix);
        period.FUpdatedTime = DateTime.Now;
        await _periodRepo.UpdateAsync(period);
    }

    public async Task<CostItemMatrixDto?> GetItemMatrixAsync(long planId, long itemId, DateTime? effectiveDate = null)
    {
        await EnsurePlanInOrgAsync(planId);

        var item = await _itemRepo.Query()
            .FirstOrDefaultAsync(i => i.FID == itemId && i.FPlanId == planId);
        if (item == null) return null;

        // 查找匹配的时间段
        var periodQuery = _periodRepo.Query().Where(p => p.FItemId == itemId);
        if (effectiveDate.HasValue)
            periodQuery = periodQuery.Where(p => p.FEffectiveDate <= effectiveDate.Value);

        var period = await periodQuery
            .OrderByDescending(p => p.FEffectiveDate)
            .FirstOrDefaultAsync();

        if (period == null || string.IsNullOrWhiteSpace(period.FMatrixJson))
            return new CostItemMatrixDto
            {
                CostItemId = (int)itemId,
                CostItemName = item.FItemName,
                PricingScope = ItemTypeToPricingScope(item.FItemType),
                Segments = new List<CostSegmentDto>()
            };

        // 反序列化矩阵 JSON
        var matrix = PricingMatrixSerializer.DeserializeCostPlan(period.FMatrixJson);

        // 查找匹配的成本项条目
        var entry = matrix.CostItems.FirstOrDefault(ci => ci.CostItemId == itemId);
        if (entry == null)
        {
            // 兼容旧格式：如果没有 costItemId 匹配，取第一个条目
            entry = matrix.CostItems.FirstOrDefault();
        }

        if (entry == null)
            return new CostItemMatrixDto
            {
                CostItemId = (int)itemId,
                CostItemName = item.FItemName,
                PricingScope = ItemTypeToPricingScope(item.FItemType),
                Segments = new List<CostSegmentDto>()
            };

        var dto = MapToCostItemMatrixDto(entry, item.FItemName);

        // 城市加收：存量数据可能缺省份ID（旧版前端未携带），读取时按城市ID回填，
        // 前端正确回显并在下次保存时落库修复
        if (dto.PricingScope == "city")
        {
            var missingCityIds = dto.Segments
                .SelectMany(s => s.Cells)
                .Where(c => c.CityId is > 0 && (c.ProvinceId is null or 0))
                .Select(c => c.CityId!.Value)
                .Distinct()
                .ToList();
            if (missingCityIds.Count > 0)
            {
                var cityProvinceMap = await _context.Set<ExpCity>()
                    .Where(c => missingCityIds.Contains(c.FID))
                    .ToDictionaryAsync(c => c.FID, c => c.FProvinceId);
                foreach (var cell in dto.Segments.SelectMany(s => s.Cells))
                {
                    if (cell.CityId is > 0 && (cell.ProvinceId is null or 0)
                        && cityProvinceMap.TryGetValue(cell.CityId.Value, out var provinceId)
                        && provinceId > 0)
                        cell.ProvinceId = provinceId;
                }
            }
        }

        return dto;
    }

    /// <summary>
    /// 验证 pricingScope 与 cells 数据的一致性
    /// </summary>
    private static void ValidatePricingScopeCells(string pricingScope, List<CostSegmentRequest> segments)
    {
        foreach (var seg in segments)
        {
            foreach (var cell in seg.Cells)
            {
                switch (pricingScope)
                {
                    case "national":
                        // 全国单价模式：cells 不应有地理维度
                        if (cell.ProvinceId.HasValue && cell.ProvinceId.Value != 0)
                            throw new InvalidOperationException("全国单价模式下单元格不应包含省份ID");
                        if (cell.CityId.HasValue)
                            throw new InvalidOperationException("全国单价模式下单元格不应包含城市ID");
                        break;
                    case "province":
                        // 省份矩阵模式：cells 应有 provinceId
                        if (!cell.ProvinceId.HasValue || cell.ProvinceId.Value == 0)
                            throw new InvalidOperationException("省份矩阵模式下单元格必须包含省份ID");
                        break;
                    case "city":
                        // 城市加收模式：允许三类行，与计费引擎 FindCellCity 的三级回退一致：
                        //   城市行（CityId/CityName + 省份）、省份回退行（仅省份）、全国回退行（无地理维度）。
                        // 城市行必须带省份ID，否则引擎按 (省份ID, 城市名) 匹配永不命中
                        var isCityRow = (cell.CityId.HasValue && cell.CityId.Value != 0)
                            || !string.IsNullOrWhiteSpace(cell.CityName);
                        if (isCityRow && (!cell.ProvinceId.HasValue || cell.ProvinceId.Value == 0))
                            throw new InvalidOperationException("城市加收模式下城市单元格必须同时包含省份ID");
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 将 ItemType 数值转换为 pricingScope 字符串
    /// </summary>
    private static string ItemTypeToPricingScope(int itemType) => itemType switch
    {
        1 => "national",
        2 => "province",
        3 => "city",
        _ => "province"
    };

    /// <summary>
    /// 将 CostItemEntry 模型映射为 CostItemMatrixDto
    /// </summary>
    private static CostItemMatrixDto MapToCostItemMatrixDto(CostItemEntry entry, string itemName)
    {
        return new CostItemMatrixDto
        {
            CostItemId = entry.CostItemId,
            CostItemName = itemName,
            PricingScope = entry.PricingScope,
            Segments = entry.Segments.Select(s => new CostSegmentDto
            {
                SegmentIndex = s.SegmentIndex,
                WeightFrom = s.WeightFrom,
                WeightTo = s.WeightTo,
                CalcMethod = s.CalcMethod,
                RoundingMethod = s.RoundingMethod,
                TruncParam = s.TruncParam,
                CeilParam = s.CeilParam,
                Cells = s.Cells.Select(c => new CostCellDto
                {
                    ProvinceId = c.ProvinceId == 0 ? null : c.ProvinceId,
                    CityId = c.CityId,
                    CityName = c.CityName,
                    BasePrice = c.BasePrice,
                    ContinuePrice = c.ContinuePrice,
                    FirstWeight = c.FirstWeight,
                    ContinueStep = c.ContinueStep,
                    RoundingMethodOverride = c.RoundingMethodOverride,
                    TruncParamOverride = c.TruncParamOverride,
                    CeilParamOverride = c.CeilParamOverride,
                }).ToList()
            }).ToList()
        };
    }

    // ==================== 城市查询 ====================

    public async Task<List<CityDto>> GetCitiesAsync(string? keyword)
    {
        var query = _context.Set<ExpCity>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(c => c.FName.Contains(keyword));

        return await query
            .OrderBy(c => c.FProvinceId).ThenBy(c => c.FName)
            .Take(50)
            .Select(c => new CityDto
            {
                Id = c.FID,
                CityName = c.FName,
                ProvinceId = c.FProvinceId,
                ProvinceName = c.FProvinceName
            })
            .ToListAsync();
    }
}
