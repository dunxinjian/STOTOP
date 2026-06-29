using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Models;

namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 成本方案缓存条目（含日期范围）
/// </summary>
public class CostPlanEntry
{
    public long PlanId { get; set; }
    public DateTime EffectiveDate { get; set; }
}

/// <summary>
/// 成本项某一时间段的矩阵快照（按运单日期取价）
/// </summary>
public class CostItemPeriod
{
    public DateTime EffectiveDate { get; set; }
    public List<PricingSegment> Segments { get; set; } = new();
    public string PricingScope { get; set; } = "province";
}

/// <summary>一口价成本项（方案项类型4）缓存条目</summary>
public class FixedPriceItemEntry
{
    /// <summary>方案成本项ID（与矩阵 entry CostItemId 同值）</summary>
    public int ItemId { get; set; }
    /// <summary>关联店铺（忽略大小写）。空集合 = 该一口价项不生效（必须显式配置店铺）。</summary>
    public HashSet<string> ShopNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>结算重量环节 1=揽收..7=最大值。一期成本链路仅有计费重量，按计费重量取价，此字段供后续打通环节重量使用。</summary>
    public int? SettlementWeightStage { get; set; }
}

/// <summary>
/// 成本缓存。
/// 内部使用 PricingSegment + PricingCell 直接存储矩阵数据，
/// 通过 PriceFormula 统一计算（支持完整进位规则）。
/// </summary>
public class CostPlanCache
{
    /// <summary>(NetworkPointId, BrandCode) → 按生效日期排序的成本方案列表</summary>
    private Dictionary<(long, string), List<CostPlanEntry>> _planIndex = new();
    /// <summary>(PlanId, CostItemId) → 按生效日期降序的时间段矩阵列表（每段含按WeightFrom排序的PricingSegment与PricingScope）</summary>
    private Dictionary<(long, int), List<CostItemPeriod>> _itemPeriodIndex = new();
    /// <summary>(PlanId, CostItemId) → 适用网点ID集合。空集合表示当前组织下所有网点均适用。</summary>
    private Dictionary<(long, int), HashSet<long>> _itemOutletIndex = new();
    /// <summary>
    /// 方案成本项ID → IsRebate。矩阵 entry.CostItemId 与方案成本项 FID 同一 ID 空间，
    /// 返利标志在全局成本项目表上且两表无外键，加载时按成本项名称映射
    /// （规范化：忽略大小写与全部内部空白，见 <see cref="NormalizeItemName"/>）。
    /// （不能按全局成本项目 FID 索引：两表各自自增，按全局 FID 查方案项 ID 是跨表错配，
    /// 返利只会因 ID 碰撞偶然生效/误伤。）
    /// </summary>
    private Dictionary<int, bool> _rebateIndex = new();
    /// <summary>(PlanId, 全局成本项编码大写) → 方案成本项ID列表（按名称映射，供一口价模式按附加项编码索引查找）</summary>
    private Dictionary<(long, string), List<int>> _codeToPlanItemIndex = new();
    /// <summary>PlanId → 一口价成本项列表（方案项类型4，按店铺命中）</summary>
    private Dictionary<long, List<FixedPriceItemEntry>> _fixedPriceIndex = new();
    /// <summary>全部一口价方案项ID（标准模式下排除这些项）</summary>
    private HashSet<int> _fixedPriceItemIds = new();
    /// <summary>PlanId → 按生效日期降序的互斥规则（一口价命中时排除的方案成本项ID集合）</summary>
    private Dictionary<long, List<(DateTime EffectiveDate, HashSet<int> ExcludedItemIds)>> _exclusionIndex = new();

    /// <summary>诊断：方案成本项名称匹配不到任何全局成本项目（脏数据，返利标志将缺失）。元素格式"方案{PlanId}/项{ItemId}:{名称}"。</summary>
    private readonly List<string> _unmatchedCostItemNames = new();
    /// <summary>诊断：规范化后重名的全局成本项目原始名称（仅首个生效，其余被丢弃，可能张冠李戴）。</summary>
    private readonly List<string> _duplicateGlobalItemNames = new();

    /// <summary>诊断属性：已加载的方案数</summary>
    public int PlanCount => _planIndex.Values.Sum(v => v.Count);
    /// <summary>诊断属性：已加载的矩阵段数</summary>
    public int SegmentCount => _itemPeriodIndex.Count;
    /// <summary>诊断属性：未匹配到全局成本项目的方案成本项（名称脏数据）</summary>
    public IReadOnlyList<string> UnmatchedCostItemNames => _unmatchedCostItemNames;
    /// <summary>诊断属性：规范化后重名的全局成本项目（重名脏数据）</summary>
    public IReadOnlyList<string> DuplicateGlobalItemNames => _duplicateGlobalItemNames;

    /// <summary>
    /// 规范化成本项名称用于跨表匹配：去除首尾及全部内部空白（含半角空格、全角空格 U+3000、制表符等）。
    /// 仅用于生成匹配 key，不改动实体的原始名称；大小写差异由索引的 OrdinalIgnoreCase 比较器处理。
    /// </summary>
    public static string NormalizeItemName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;
        return new string(name.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    /// <summary>
    /// 构建"全局成本项目规范化名称 → 项"索引（StringComparer.OrdinalIgnoreCase）。
    /// 规范化后重名只保留首个（与历史 TryAdd 行为一致），其余原始名称写入 <paramref name="duplicateNames"/> 供诊断。
    /// 空白名称跳过（无法作为匹配 key）。
    /// </summary>
    public static Dictionary<string, ExpCostItem> BuildGlobalItemIndex(
        IReadOnlyList<ExpCostItem> globalItems,
        out List<string> duplicateNames)
    {
        var index = new Dictionary<string, ExpCostItem>(StringComparer.OrdinalIgnoreCase);
        duplicateNames = new List<string>();
        foreach (var item in globalItems)
        {
            var key = NormalizeItemName(item.FName);
            if (key.Length == 0)
                continue;
            if (!index.TryAdd(key, item))
                duplicateNames.Add(item.FName);
        }
        return index;
    }

    public async Task LoadAsync(
        IRepository<ExpCostPlan> costPlanRepo,
        IRepository<ExpCostItem> costItemRepo,
        long orgId)
    {
        // 加载全局成本项目，建立"规范化名称 → 全局项"映射；方案成本项靠名称匹配取返利标志与编码。
        // 规范化忽略大小写与内部空白，避免两表名称的大小写/全角半角空格差异导致返利标志丢失。
        var costItems = await costItemRepo.Query().ToListAsync();
        var globalItemByName = BuildGlobalItemIndex(costItems, out var duplicateGlobalNames);
        _duplicateGlobalItemNames.AddRange(duplicateGlobalNames);

        // 加载当前组织可用的启用成本方案（含成本项及时间段）。
        // FOrgId=0 视为全局默认方案，组织方案优先级在同品牌下更高。
        var plans = await costPlanRepo.Query()
            .Where(p => p.FStatus == 1 && (p.FOrgId == orgId || p.FOrgId == 0))
            .Include(p => p.Items)
                .ThenInclude(i => i.Periods)
            .Include(p => p.Items)
                .ThenInclude(i => i.Outlets)
            .Include(p => p.Items)
                .ThenInclude(i => i.Shops)
            .Include(p => p.Exclusions)
            .ToListAsync();

        foreach (var plan in plans)
        {
            var planHasSegments = false;

            // 互斥规则：一口价命中时按运单日期取生效规则，排除规则内的方案成本项
            foreach (var exclusion in plan.Exclusions)
            {
                var excludedIds = ParseExcludedItemIds(exclusion.FExclusionRuleJson);
                if (excludedIds.Count == 0)
                    continue;

                if (!_exclusionIndex.TryGetValue(plan.FID, out var exclusionList))
                {
                    exclusionList = new List<(DateTime, HashSet<int>)>();
                    _exclusionIndex[plan.FID] = exclusionList;
                }
                exclusionList.Add((exclusion.FEffectiveDate.Date, excludedIds));
            }

            // 遍历成本项，加载所有时间段矩阵（按运单日期取价，不再用 DateTime.Now 锁定单一期间）
            foreach (var item in plan.Items)
            {
                // 按规范化名称匹配全局成本项目，注册返利标志与编码索引（key 均为方案成本项 FID）
                if (globalItemByName.TryGetValue(NormalizeItemName(item.FItemName), out var globalItem))
                {
                    _rebateIndex[(int)item.FID] = globalItem.FIsRebate;
                    if (!string.IsNullOrEmpty(globalItem.FCode))
                    {
                        var codeKey = (plan.FID, globalItem.FCode.ToUpperInvariant());
                        if (!_codeToPlanItemIndex.TryGetValue(codeKey, out var codeItems))
                        {
                            codeItems = new List<int>();
                            _codeToPlanItemIndex[codeKey] = codeItems;
                        }
                        codeItems.Add((int)item.FID);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(item.FItemName))
                {
                    // 名称匹配不到全局项：返利标志缺失（GetValueOrDefault 默认 false，返利会被当成正向成本），记为脏数据
                    _unmatchedCostItemNames.Add($"方案{plan.FID}/项{item.FID}:{item.FItemName}");
                }

                // 一口价成本项（类型4）：按关联店铺命中，与互斥规则配合替代标准成本项
                if (item.FItemType == 4)
                {
                    var fixedEntry = new FixedPriceItemEntry
                    {
                        ItemId = (int)item.FID,
                        SettlementWeightStage = item.FSettlementWeightStage
                    };
                    foreach (var shop in item.Shops)
                    {
                        if (!string.IsNullOrWhiteSpace(shop.FShopName))
                            fixedEntry.ShopNames.Add(shop.FShopName.Trim());
                    }

                    if (!_fixedPriceIndex.TryGetValue(plan.FID, out var fixedList))
                    {
                        fixedList = new List<FixedPriceItemEntry>();
                        _fixedPriceIndex[plan.FID] = fixedList;
                    }
                    fixedList.Add(fixedEntry);
                    _fixedPriceItemIds.Add((int)item.FID);
                }

                // 该成本项（item）的网点适用集合：空集合=当前组织所有网点适用；非空=仅指定网点适用
                var itemOutlets = item.Outlets
                    .Select(o => o.FOutletId)
                    .Where(id => id > 0)
                    .ToHashSet();

                // 遍历该成本项的全部时间段（含未来期间，按运单日期在计费时选取）
                foreach (var period in item.Periods)
                {
                    if (string.IsNullOrWhiteSpace(period.FMatrixJson))
                        continue;

                    var matrix = PricingMatrixSerializer.DeserializeCostPlan(period.FMatrixJson);

                    foreach (var entry in matrix.CostItems)
                    {
                        var sortedSegments = entry.Segments
                            .OrderBy(s => s.WeightFrom ?? 0m)
                            .ToList();
                        if (sortedSegments.Count == 0)
                            continue;

                        var key = (plan.FID, entry.CostItemId);

                        if (!_itemPeriodIndex.TryGetValue(key, out var periodList))
                        {
                            periodList = new List<CostItemPeriod>();
                            _itemPeriodIndex[key] = periodList;
                        }
                        periodList.Add(new CostItemPeriod
                        {
                            EffectiveDate = period.FEffectiveDate,
                            Segments = sortedSegments,
                            PricingScope = entry.PricingScope ?? "province"
                        });
                        planHasSegments = true;

                        // 同一全局成本项被同方案多个成本项引用时合并网点集合：任一为空(=全部)则视为全部适用，否则取并集
                        if (_itemOutletIndex.TryGetValue(key, out var existingOutlets))
                        {
                            if (existingOutlets.Count != 0 && itemOutlets.Count != 0)
                                existingOutlets.UnionWith(itemOutlets);
                            else
                                existingOutlets.Clear(); // 空集合=全部适用
                        }
                        else
                        {
                            _itemOutletIndex[key] = new HashSet<long>(itemOutlets);
                        }
                    }
                }
            }

            if (planHasSegments)
            {
                // 注册到 _planIndex：key=(0, BrandCode)，表示"所有网点"的默认方案。
                var planKey = (0L, plan.FBrandCode);
                if (!_planIndex.ContainsKey(planKey))
                    _planIndex[planKey] = new List<CostPlanEntry>();

                _planIndex[planKey].Add(new CostPlanEntry
                {
                    PlanId = plan.FID,
                    EffectiveDate = plan.FOrgId == orgId ? DateTime.MinValue.AddDays(1) : DateTime.MinValue
                });
            }
        }

        // 每个 (plan, costItem) 的时间段按生效日期降序，便于计费时取首个 <= 运单日期 的期间
        foreach (var periodList in _itemPeriodIndex.Values)
            periodList.Sort((a, b) => b.EffectiveDate.CompareTo(a.EffectiveDate));

        // 互斥规则按生效日期降序，取首个 <= 运单日期 的规则
        foreach (var exclusionList in _exclusionIndex.Values)
            exclusionList.Sort((a, b) => b.EffectiveDate.CompareTo(a.EffectiveDate));

        // 按 EffectiveDate 降序排列（确保 ResolvePlanId 取最新的）
        foreach (var entries in _planIndex.Values)
        {
            entries.Sort((a, b) => b.EffectiveDate.CompareTo(a.EffectiveDate));
        }
    }

    public CostCalcResult CalcAllCosts(
        long networkPointId, string brandCode, int provinceId, string? cityName,
        decimal billableWeight, DateTime waybillDate, string? shopName = null)
    {
        var result = new CostCalcResult();

        // 两级回退：精确网点方案优先，无则回退到默认方案 (NetworkPointId=0)
        var planId = ResolvePlanId(networkPointId, brandCode, waybillDate);
        if (planId == null)
            return result;

        // 一口价模式判定：运单店铺命中方案内某一口价项的关联店铺（未配置店铺的一口价项不生效）
        var fixedPriceItem = ResolveFixedPriceItem(planId.Value, shopName);
        var excludedItemIds = fixedPriceItem != null
            ? ResolveExclusions(planId.Value, waybillDate)
            : null;
        result.CostMode = fixedPriceItem != null ? 2 : 1;
        result.FixedPriceItemId = fixedPriceItem?.ItemId;

        // 获取该方案所有成本项
        var costItemIds = new HashSet<int>();
        foreach (var key in _itemPeriodIndex.Keys)
        {
            if (key.Item1 == planId.Value)
                costItemIds.Add(key.Item2);
        }

        foreach (var costItemId in costItemIds)
        {
            if (!ShouldCalcItem(costItemId, fixedPriceItem, excludedItemIds))
                continue;

            if (!CostItemAppliesToNetworkPoint(planId.Value, costItemId, networkPointId))
                continue;

            var amount = CalcSingleCost(planId.Value, costItemId, provinceId, cityName, billableWeight, waybillDate);
            if (amount != 0)
            {
                var isRebate = _rebateIndex.GetValueOrDefault(costItemId);
                result.Items.Add((costItemId, isRebate ? -Math.Abs(amount) : amount, isRebate));
            }
        }

        return result;
    }

    /// <summary>
    /// 一口价互斥：标准模式跳过全部一口价项；一口价模式只算命中的一口价项，
    /// 其余标准项中被互斥规则（excludedCostItemIds）列出的跳过、未列出的照常叠加。
    /// </summary>
    private bool ShouldCalcItem(int costItemId, FixedPriceItemEntry? fixedPriceItem, HashSet<int>? excludedItemIds)
    {
        var isFixedPrice = _fixedPriceItemIds.Contains(costItemId);

        if (fixedPriceItem == null)
            return !isFixedPrice;

        if (isFixedPrice)
            return costItemId == fixedPriceItem.ItemId;

        return excludedItemIds == null || !excludedItemIds.Contains(costItemId);
    }

    /// <summary>店铺命中方案内的一口价成本项（关联店铺为空的一口价项视为未启用）</summary>
    private FixedPriceItemEntry? ResolveFixedPriceItem(long planId, string? shopName)
    {
        if (string.IsNullOrWhiteSpace(shopName)
            || !_fixedPriceIndex.TryGetValue(planId, out var entries))
            return null;

        var trimmed = shopName.Trim();
        return entries.FirstOrDefault(e => e.ShopNames.Count > 0 && e.ShopNames.Contains(trimmed));
    }

    /// <summary>按运单日期取生效的互斥规则（列表已按日期降序）</summary>
    private HashSet<int>? ResolveExclusions(long planId, DateTime waybillDate)
    {
        if (!_exclusionIndex.TryGetValue(planId, out var exclusions))
            return null;

        return exclusions.FirstOrDefault(e => e.EffectiveDate <= waybillDate.Date).ExcludedItemIds;
    }

    private static HashSet<int> ParseExcludedItemIds(string? exclusionRuleJson)
    {
        var result = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(exclusionRuleJson))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(exclusionRuleJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("excludedCostItemIds", out var arr)
                && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in arr.EnumerateArray())
                {
                    if (elem.TryGetInt32(out var id))
                        result.Add(id);
                }
            }
        }
        catch (JsonException)
        {
            // 写入侧已校验；坏数据按无互斥规则处理
        }

        return result;
    }

    public CostExplainResult ExplainAllCosts(
        long networkPointId, string brandCode, int provinceId, string? cityName,
        decimal billableWeight, DateTime waybillDate, string? shopName = null)
    {
        var result = new CostExplainResult();
        var planId = ResolvePlanId(networkPointId, brandCode, waybillDate);
        result.PlanId = planId;

        if (planId == null)
        {
            result.ConfigurationIssues.Add(
                $"未命中成本方案：网点={networkPointId}, 品牌={brandCode}, 日期={waybillDate:yyyy-MM-dd}");
            return result;
        }

        var fixedPriceItem = ResolveFixedPriceItem(planId.Value, shopName);
        var excludedItemIds = fixedPriceItem != null
            ? ResolveExclusions(planId.Value, waybillDate)
            : null;
        result.CostMode = fixedPriceItem != null ? 2 : 1;
        result.FixedPriceItemId = fixedPriceItem?.ItemId;
        if (fixedPriceItem != null)
        {
            result.MatchNotes.Add($"店铺[{shopName}]命中一口价成本项 {fixedPriceItem.ItemId}，按一口价模式计算");
            if (excludedItemIds is { Count: > 0 })
                result.MatchNotes.Add($"互斥规则生效：排除成本项 [{string.Join(", ", excludedItemIds.OrderBy(id => id))}]");
            else
                result.MatchNotes.Add("方案未配置生效的互斥规则，其余成本项照常叠加");
        }

        var costItemIds = _itemPeriodIndex.Keys
            .Where(k => k.Item1 == planId.Value)
            .Select(k => k.Item2)
            .Distinct()
            .ToList();

        foreach (var costItemId in costItemIds)
        {
            if (!ShouldCalcItem(costItemId, fixedPriceItem, excludedItemIds))
            {
                var reason = _fixedPriceItemIds.Contains(costItemId)
                    ? (fixedPriceItem == null ? "一口价项（店铺未命中，标准模式不计）" : "其他一口价项")
                    : "被一口价互斥规则排除";
                result.MatchNotes.Add($"成本项 {costItemId} 跳过：{reason}");
                continue;
            }

            if (!CostItemAppliesToNetworkPoint(planId.Value, costItemId, networkPointId))
            {
                result.ConfigurationIssues.Add($"成本项不适用于当前网点：成本项={costItemId}, 网点={networkPointId}");
                continue;
            }

            var item = ExplainSingleCost(planId.Value, costItemId, provinceId, cityName, billableWeight, waybillDate);
            if (item != null)
                result.Items.Add(item);
        }

        if (result.Items.Count == 0 && result.ConfigurationIssues.Count == 0)
            result.ConfigurationIssues.Add("命中成本方案但未命中任何成本项矩阵");

        return result;
    }

    /// <summary>
    /// 解析方案ID：先精确匹配 (NetworkPointId, BrandCode)，无匹配则回退到 (0, BrandCode) 默认方案。
    /// 两级均按运单日期筛选最新生效方案。
    /// </summary>
    private long? ResolvePlanId(long networkPointId, string brandCode, DateTime waybillDate)
    {
        // 1) 精确匹配网点
        if (_planIndex.TryGetValue((networkPointId, brandCode), out var entries))
        {
            var matched = entries.FirstOrDefault(e => e.EffectiveDate <= waybillDate);
            if (matched != null)
                return matched.PlanId;
        }

        // 2) 回退到默认方案（NetworkPointId=0 表示"所有网点"）
        if (networkPointId != 0 && _planIndex.TryGetValue((0L, brandCode), out var defaults))
        {
            var matched = defaults.FirstOrDefault(e => e.EffectiveDate <= waybillDate);
            if (matched != null)
                return matched.PlanId;
        }

        return null;
    }

    /// <summary>
    /// 仅计算指定编码的成本项（用于一口价模式下叠加几项附加成本）。
    /// 编码经"全局成本项目名称 == 方案成本项名称"映射到方案成本项后计算。
    /// 未配置或金额为 0 的项不会返回。未识别的编码静默跳过。
    /// </summary>
    public List<(int CostItemId, decimal Amount)> CalcSelectedCosts(
        long planId,
        string[] costItemCodes,
        int provinceId,
        string? cityName,
        decimal billableWeight,
        DateTime waybillDate)
    {
        var results = new List<(int CostItemId, decimal Amount)>();
        foreach (var code in costItemCodes)
        {
            if (string.IsNullOrWhiteSpace(code)
                || !_codeToPlanItemIndex.TryGetValue((planId, code.Trim().ToUpperInvariant()), out var costItemIds))
                continue;

            foreach (var costItemId in costItemIds)
            {
                // 未配置该项的明细时 CalcSingleCost 返回 0，不报错
                var amount = CalcSingleCost(planId, costItemId, provinceId, cityName, billableWeight, waybillDate);
                if (amount == 0)
                    continue;

                var isRebate = _rebateIndex.GetValueOrDefault(costItemId);
                results.Add((costItemId, isRebate ? -Math.Abs(amount) : amount));
            }
        }
        return results;
    }

    /// <summary>
    /// 公开方案解析（一口价模式下仍需按标准方式查找附加项所属成本方案）。
    /// </summary>
    public long? ResolveCostPlanId(long networkPointId, string brandCode, DateTime waybillDate)
        => ResolvePlanId(networkPointId, brandCode, waybillDate);

    private bool CostItemAppliesToNetworkPoint(long planId, int costItemId, long networkPointId)
    {
        if (!_itemOutletIndex.TryGetValue((planId, costItemId), out var outletIds))
            return true;

        return outletIds.Count == 0 || outletIds.Contains(networkPointId);
    }

    /// <summary>按运单日期取该成本项生效的时间段（首个 EffectiveDate &lt;= 运单日期，列表已按日期降序）</summary>
    private CostItemPeriod? ResolvePeriod(long planId, int costItemId, DateTime waybillDate)
    {
        if (!_itemPeriodIndex.TryGetValue((planId, costItemId), out var periods))
            return null;
        return periods.FirstOrDefault(p => p.EffectiveDate <= waybillDate);
    }

    private decimal CalcSingleCost(long planId, int costItemId, int provinceId, string? cityName, decimal billableWeight, DateTime waybillDate)
    {
        var period = ResolvePeriod(planId, costItemId, waybillDate);
        if (period == null)
            return 0;

        // 在重量段中匹配
        var segment = FindSegment(period.Segments, billableWeight);
        if (segment == null)
            return 0;

        // 根据 pricingScope 查找矩阵单元格
        var cell = FindCell(segment, period.PricingScope, provinceId, cityName);
        if (cell == null)
            return 0;

        // 统一公式计算（含进位规则）
        return PriceFormula.Calculate(billableWeight, segment, cell);
    }

    private CostItemExplainResult? ExplainSingleCost(long planId, int costItemId, int provinceId, string? cityName, decimal billableWeight, DateTime waybillDate)
    {
        var period = ResolvePeriod(planId, costItemId, waybillDate);
        if (period == null)
            return null;

        var segment = FindSegment(period.Segments, billableWeight);
        if (segment == null)
            return null;

        var pricingScope = period.PricingScope;
        var cell = FindCell(segment, pricingScope, provinceId, cityName);
        if (cell == null)
            return null;

        var formula = PriceFormula.Explain(billableWeight, segment, cell);
        var isRebate = _rebateIndex.GetValueOrDefault(costItemId);
        var amount = isRebate ? -Math.Abs(formula.Amount) : formula.Amount;

        return new CostItemExplainResult
        {
            CostItemId = costItemId,
            Amount = amount,
            IsRebate = isRebate,
            MatchPath = $"{pricingScope}: province={provinceId}, city={cityName ?? ""}",
            Formula = formula
        };
    }

    private static PricingSegment? FindSegment(List<PricingSegment> segments, decimal billableWeight)
    {
        foreach (var s in segments)
        {
            var from = s.WeightFrom ?? 0m;
            var to = s.WeightTo ?? decimal.MaxValue;
            if (billableWeight >= from && billableWeight < to)
                return s;
        }
        // 如果没有匹配的重量段，取最后一个（适用于无重量段的按单计费）
        return segments.Count == 1 && segments[0].WeightFrom == null ? segments[0] : null;
    }

    /// <summary>
    /// 根据 pricingScope 查找单元格：
    /// national: 直接命中（任何目的地都匹配）
    /// province: 按目的省份匹配，回退到全国（ProvinceId=0）
    /// city: 按目的城市匹配，回退到省份，再回退到全国
    /// </summary>
    private static PricingCell? FindCell(PricingSegment segment, string pricingScope, int provinceId, string? cityName)
    {
        return pricingScope.ToLowerInvariant() switch
        {
            "national" => FindCellNational(segment),
            "city" => FindCellCity(segment, provinceId, cityName),
            _ => FindCellProvince(segment, provinceId) // province 及旧数据默认
        };
    }

    /// <summary>
    /// 全国单价模式：直接返回第一个单元格（无地理维度）
    /// </summary>
    private static PricingCell? FindCellNational(PricingSegment segment)
    {
        return segment.Cells.FirstOrDefault();
    }

    /// <summary>
    /// 省份矩阵模式：按省份匹配，回退到全国（ProvinceId=0）
    /// </summary>
    private static PricingCell? FindCellProvince(PricingSegment segment, int provinceId)
    {
        // 1. 精确省份匹配
        var byProvince = segment.Cells.FirstOrDefault(c =>
            c.ProvinceId == provinceId &&
            !c.CityId.HasValue &&
            string.IsNullOrEmpty(c.CityName));
        if (byProvince != null)
            return byProvince;

        // 2. 全国回退（ProvinceId=0）
        if (provinceId != 0)
        {
            var fallback = segment.Cells.FirstOrDefault(c =>
                c.ProvinceId == 0 &&
                !c.CityId.HasValue &&
                string.IsNullOrEmpty(c.CityName));
            if (fallback != null)
                return fallback;
        }

        return null;
    }

    /// <summary>
    /// 城市加收模式：三级回退（城市名称关键字 → 省份 → 全国）
    /// </summary>
    private static PricingCell? FindCellCity(PricingSegment segment, int provinceId, string? cityName)
    {
        // 1. 城市加收按名称关键字匹配。成本输入只有城市名，没有城市ID。
        if (!string.IsNullOrEmpty(cityName))
        {
            var byCityName = segment.Cells.FirstOrDefault(c =>
                c.ProvinceId == provinceId &&
                !string.IsNullOrWhiteSpace(c.CityName) &&
                CityNameMatches(c.CityName, cityName));
            if (byCityName != null)
                return byCityName;
        }

        // 2. 回退到省份匹配
        var byProvince = segment.Cells.FirstOrDefault(c =>
            c.ProvinceId == provinceId &&
            !c.CityId.HasValue &&
            string.IsNullOrEmpty(c.CityName));
        if (byProvince != null)
            return byProvince;

        // 3. 回退到全国（ProvinceId=0）
        if (provinceId != 0)
        {
            var fallback = segment.Cells.FirstOrDefault(c =>
                c.ProvinceId == 0 &&
                !c.CityId.HasValue &&
                string.IsNullOrEmpty(c.CityName));
            if (fallback != null)
                return fallback;
        }

        return null;
    }

    private static bool CityNameMatches(string? configuredName, string? inputName)
    {
        if (string.IsNullOrWhiteSpace(configuredName) || string.IsNullOrWhiteSpace(inputName))
            return false;

        var configured = configuredName.Trim();
        var input = inputName.Trim();

        if (string.Equals(configured, input, StringComparison.OrdinalIgnoreCase))
            return true;

        var configuredKey = NormalizeCityKeyword(configured);
        var inputKey = NormalizeCityKeyword(input);

        if (string.IsNullOrWhiteSpace(configuredKey) || string.IsNullOrWhiteSpace(inputKey))
            return false;

        if (string.Equals(configuredKey, inputKey, StringComparison.OrdinalIgnoreCase))
            return true;

        return configuredKey.Length >= 2
            && inputKey.Length >= 2
            && (configuredKey.Contains(inputKey, StringComparison.OrdinalIgnoreCase)
                || inputKey.Contains(configuredKey, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCityKeyword(string cityName)
    {
        var name = new string(cityName.Trim().Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (name.Length == 0)
            return name;

        var autonomousIndex = name.IndexOf("自治州", StringComparison.Ordinal);
        if (autonomousIndex > 0)
        {
            name = StripTrailingEthnicNames(name[..autonomousIndex]);
        }
        else
        {
            string[] suffixes =
            [
                "特别行政区", "地区", "林区", "盟", "自治县", "县", "市", "州"
            ];

            foreach (var suffix in suffixes.OrderByDescending(s => s.Length))
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
                {
                    name = name[..^suffix.Length];
                    break;
                }
            }
        }

        return name;
    }

    private static string StripTrailingEthnicNames(string value)
    {
        string[] ethnicNames =
        [
            "柯尔克孜族", "哈萨克族", "塔吉克族", "维吾尔族", "俄罗斯族", "乌孜别克族",
            "塔塔尔族", "达斡尔族", "鄂温克族", "鄂伦春族", "保安族", "裕固族",
            "东乡族", "撒拉族", "土家族", "布依族", "仡佬族", "仫佬族",
            "毛南族", "景颇族", "傈僳族", "独龙族", "普米族", "纳西族",
            "哈尼族", "朝鲜族", "蒙古族", "蒙古", "柯尔克孜", "哈萨克",
            "汉族", "回族", "藏族", "苗族", "彝族", "壮族", "侗族",
            "瑶族", "白族", "傣族", "黎族", "佤族", "畲族", "水族",
            "羌族", "怒族", "京族", "门巴族", "珞巴族", "基诺族"
        ];

        var result = value;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var ethnicName in ethnicNames.OrderByDescending(s => s.Length))
            {
                if (result.EndsWith(ethnicName, StringComparison.Ordinal) && result.Length > ethnicName.Length)
                {
                    result = result[..^ethnicName.Length];
                    changed = true;
                    break;
                }
            }
        }

        return result;
    }
}

/// <summary>CalcAllCosts 计算结果（含一口价模式标记）</summary>
public class CostCalcResult
{
    public List<(int CostItemId, decimal Amount, bool IsRebate)> Items { get; } = [];
    /// <summary>1=标准 2=一口价（写入 ExpBillingResult.FCostMode）</summary>
    public int CostMode { get; set; } = 1;
    /// <summary>一口价模式下命中的方案成本项ID</summary>
    public int? FixedPriceItemId { get; set; }
}

public class CostExplainResult
{
    public long? PlanId { get; set; }
    /// <summary>1=标准 2=一口价</summary>
    public int CostMode { get; set; } = 1;
    /// <summary>一口价模式下命中的方案成本项ID</summary>
    public int? FixedPriceItemId { get; set; }
    /// <summary>命中/跳过说明（一口价命中、互斥排除等）</summary>
    public List<string> MatchNotes { get; set; } = [];
    public List<CostItemExplainResult> Items { get; set; } = [];
    public List<string> ConfigurationIssues { get; set; } = [];
    public decimal TotalAmount => Items.Sum(i => i.Amount);
}

public class CostItemExplainResult
{
    public int CostItemId { get; set; }
    public decimal Amount { get; set; }
    public bool IsRebate { get; set; }
    public string MatchPath { get; set; } = string.Empty;
    public PriceFormulaExplainResult? Formula { get; set; }
}
