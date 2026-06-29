using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Services.PricePlan;

namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 附加费缓存 — 三级作用域模型（全局/业务对象/报价），就近优先覆盖
/// </summary>
public class SurchargeCache
{
    // 全局索引: (brandCode, networkPointCode, surchargeType)
    private readonly Dictionary<(string, string?, int), List<SurchargeCacheEntry>> _globalIndex = new();

    // 业务对象索引: (brandCode, networkPointCode, clientType, clientId, surchargeType)
    private readonly Dictionary<(string, string?, string, string, int), List<SurchargeCacheEntry>> _clientIndex = new();

    // 报价索引: (brandCode, networkPointCode, quotationId, surchargeType)
    private readonly Dictionary<(string, string?, long, int), List<SurchargeCacheEntry>> _quotationIndex = new();

    // 已知的所有加收类型
    private HashSet<int> _allSurchargeTypes = new();

    public async Task LoadAsync(
        IRepository<ExpPriceSurcharge> surchargeRepo,
        IRepository<ExpPriceSurchargeItem> itemRepo,
        IRepository<ExpPriceSurchargeItemDest> destRepo,
        long orgId)
    {
        _globalIndex.Clear();
        _clientIndex.Clear();
        _quotationIndex.Clear();

        // 加载所有启用的加收规则（含作用域和配置项），显式按组织过滤（纵深防御）
        var surcharges = await surchargeRepo.Query()
            .Where(s => s.FIsActive && s.FOrgId == orgId)
            .Include(s => s.Scopes)
            .Include(s => s.Items)
            .ToListAsync();

        // 加载目的地
        var dests = await destRepo.Query().ToListAsync();
        var destMap = dests.GroupBy(d => d.FSurchargeItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var s in surcharges)
        {
            _allSurchargeTypes.Add(s.FSurchargeType);

            var entry = new SurchargeCacheEntry
            {
                SurchargeId = s.FID,
                SurchargeType = s.FSurchargeType,
                EffectiveDate = s.FEffectiveDate,
                ExpiryDate = s.FExpiryDate,
                Scope = s.FScope,
                Items = new()
            };

            // 构建配置项
            foreach (var item in s.Items.OrderBy(i => i.FSortOrder))
            {
                var itemEntry = new SurchargeItemCacheEntry
                {
                    CalcMethod = item.FCalcMethod,
                    WeightRoundingMethod = item.FWeightRoundingMethod,
                    WeightFrom = item.FWeightFrom,
                    WeightTo = item.FWeightTo,
                    DailyVolumeFrom = item.FDailyVolumeFrom,
                    DailyVolumeTo = item.FDailyVolumeTo,
                    Amount = item.FAmount,
                    Provinces = new HashSet<int>()
                };

                if (destMap.TryGetValue(item.FID, out var itemDests))
                {
                    foreach (var d in itemDests)
                    {
                        if (d.FProvinceId.HasValue)
                            itemEntry.Provinces.Add(d.FProvinceId.Value);
                    }
                }

                entry.Items.Add(itemEntry);
            }

            // 按作用域分流到不同索引
            switch (s.FScope)
            {
                case 0: // 全局
                    AddToGlobalIndex(s.FBrandCode, s.FNetworkPointCode, s.FSurchargeType, entry);
                    break;

                case 1: // 业务对象级
                    foreach (var scope in s.Scopes)
                    {
                        AddToClientIndex(s.FBrandCode, s.FNetworkPointCode,
                            scope.FLinkedType, scope.FLinkedId, s.FSurchargeType, entry);
                    }
                    break;

                case 2: // 报价级
                    foreach (var scope in s.Scopes.Where(sc => sc.FLinkedType == "QUOTATION"))
                    {
                        if (long.TryParse(scope.FLinkedId, out var quotationId))
                        {
                            AddToQuotationIndex(s.FBrandCode, s.FNetworkPointCode,
                                quotationId, s.FSurchargeType, entry);
                        }
                    }
                    break;
            }
        }

        // 每个 key 的 list 按 EffectiveDate DESC 排序
        foreach (var list in _globalIndex.Values)
            list.Sort((a, b) => b.EffectiveDate.CompareTo(a.EffectiveDate));
        foreach (var list in _clientIndex.Values)
            list.Sort((a, b) => b.EffectiveDate.CompareTo(a.EffectiveDate));
        foreach (var list in _quotationIndex.Values)
            list.Sort((a, b) => b.EffectiveDate.CompareTo(a.EffectiveDate));
    }

    /// <summary>
    /// 计算附加费总额（就近优先覆盖：报价级 > 业务对象级 > 全局，不同类型叠加）
    /// </summary>
    public decimal CalcSurcharges(
        string brandCode, string? networkPointCode,
        string clientType, string clientId,
        long quotationId,
        int provinceId, decimal billableWeight, DateTime waybillDate, int dailyVolume)
    {
        decimal total = 0;

        foreach (var surchargeType in _allSurchargeTypes)
        {
            var matched = FindByQuotation(brandCode, networkPointCode, quotationId, surchargeType, waybillDate)
                       ?? FindByClient(brandCode, networkPointCode, clientType, clientId, surchargeType, waybillDate)
                       ?? FindGlobal(brandCode, networkPointCode, surchargeType, waybillDate);

            if (matched != null)
                total += CalcEntry(matched, provinceId, billableWeight, dailyVolume, surchargeType);
        }

        return total;
    }

    // 生效区间判断：生效日期已到，且未过失效日期（失效日期为空=长期有效，失效日期当天起失效）
    private static bool IsEntryEffective(SurchargeCacheEntry e, DateTime date)
        => e.EffectiveDate <= date && (e.ExpiryDate == null || date < e.ExpiryDate.Value);

    // 每级查找做两次：先精确匹配运单网点，未命中再回退网点编码为 null 的规则
    // （FNetworkPointCode=null 表示"品牌全局"，精确 tuple 匹配会让全局规则永远匹配不到带网点的运单）

    private SurchargeCacheEntry? FindByQuotation(
        string brandCode, string? networkPointCode, long quotationId, int surchargeType, DateTime date)
    {
        return FindByQuotationExact(brandCode, networkPointCode, quotationId, surchargeType, date)
            ?? (networkPointCode != null
                ? FindByQuotationExact(brandCode, null, quotationId, surchargeType, date)
                : null);
    }

    private SurchargeCacheEntry? FindByQuotationExact(
        string brandCode, string? networkPointCode, long quotationId, int surchargeType, DateTime date)
    {
        var key = (brandCode, networkPointCode, quotationId, surchargeType);
        if (!_quotationIndex.TryGetValue(key, out var list)) return null;
        return list.FirstOrDefault(e => IsEntryEffective(e, date));
    }

    private SurchargeCacheEntry? FindByClient(
        string brandCode, string? networkPointCode, string clientType, string clientId, int surchargeType, DateTime date)
    {
        return FindByClientExact(brandCode, networkPointCode, clientType, clientId, surchargeType, date)
            ?? (networkPointCode != null
                ? FindByClientExact(brandCode, null, clientType, clientId, surchargeType, date)
                : null);
    }

    private SurchargeCacheEntry? FindByClientExact(
        string brandCode, string? networkPointCode, string clientType, string clientId, int surchargeType, DateTime date)
    {
        var key = (brandCode, networkPointCode, clientType, clientId, surchargeType);
        if (!_clientIndex.TryGetValue(key, out var list)) return null;
        return list.FirstOrDefault(e => IsEntryEffective(e, date));
    }

    private SurchargeCacheEntry? FindGlobal(
        string brandCode, string? networkPointCode, int surchargeType, DateTime date)
    {
        return FindGlobalExact(brandCode, networkPointCode, surchargeType, date)
            ?? (networkPointCode != null
                ? FindGlobalExact(brandCode, null, surchargeType, date)
                : null);
    }

    private SurchargeCacheEntry? FindGlobalExact(
        string brandCode, string? networkPointCode, int surchargeType, DateTime date)
    {
        var key = (brandCode, networkPointCode, surchargeType);
        if (!_globalIndex.TryGetValue(key, out var list)) return null;
        return list.FirstOrDefault(e => IsEntryEffective(e, date));
    }

    /// <summary>
    /// 计算单个加收规则的金额（保留原有业务逻辑）
    /// </summary>
    private static decimal CalcEntry(SurchargeCacheEntry entry, int provinceId,
        decimal billableWeight, int dailyVolume, int surchargeType)
    {
        decimal amount = 0;

        foreach (var item in entry.Items)
        {
            // 目的地匹配（无记录 = 全国）
            if (item.Provinces.Count > 0 && !item.Provinces.Contains(provinceId))
                continue;

            // 应用配置项的重量进位方式后再做区间匹配与按重计费（配置了进位却忽略会少收）
            var roundedWeight = item.WeightRoundingMethod is > 0
                ? WeightRoundingHelper.RoundWeight(billableWeight, item.WeightRoundingMethod.Value, null, null)
                : billableWeight;

            // 重量范围
            var from = item.WeightFrom ?? 0;
            var to = item.WeightTo ?? decimal.MaxValue;
            if (roundedWeight < from || roundedWeight >= to)
                continue;

            // 单量加收(Type=6)
            if (surchargeType == 6)
            {
                var volFrom = item.DailyVolumeFrom ?? 0;
                var volTo = item.DailyVolumeTo ?? int.MaxValue;
                if (dailyVolume < volFrom || dailyVolume >= volTo)
                    continue;
            }

            // 按 CalcMethod 计算金额
            amount += item.CalcMethod switch
            {
                1 => item.Amount, // 固定金额
                2 => roundedWeight * item.Amount, // 按重量
                _ => item.Amount
            };
        }

        return amount;
    }

    #region Index helpers

    private void AddToGlobalIndex(string brandCode, string? networkPointCode, int surchargeType, SurchargeCacheEntry entry)
    {
        var key = (brandCode, networkPointCode, surchargeType);
        if (!_globalIndex.TryGetValue(key, out var list))
        {
            list = new List<SurchargeCacheEntry>();
            _globalIndex[key] = list;
        }
        list.Add(entry);
    }

    private void AddToClientIndex(string brandCode, string? networkPointCode, string clientType, string clientId, int surchargeType, SurchargeCacheEntry entry)
    {
        var key = (brandCode, networkPointCode, clientType, clientId, surchargeType);
        if (!_clientIndex.TryGetValue(key, out var list))
        {
            list = new List<SurchargeCacheEntry>();
            _clientIndex[key] = list;
        }
        list.Add(entry);
    }

    private void AddToQuotationIndex(string brandCode, string? networkPointCode, long quotationId, int surchargeType, SurchargeCacheEntry entry)
    {
        var key = (brandCode, networkPointCode, quotationId, surchargeType);
        if (!_quotationIndex.TryGetValue(key, out var list))
        {
            list = new List<SurchargeCacheEntry>();
            _quotationIndex[key] = list;
        }
        list.Add(entry);
    }

    #endregion

    #region Cache entry types

    private class SurchargeCacheEntry
    {
        public long SurchargeId { get; set; }
        public int SurchargeType { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int Scope { get; set; }
        public List<SurchargeItemCacheEntry> Items { get; set; } = new();
    }

    private class SurchargeItemCacheEntry
    {
        public int CalcMethod { get; set; }
        public int? WeightRoundingMethod { get; set; }
        public decimal? WeightFrom { get; set; }
        public decimal? WeightTo { get; set; }
        public int? DailyVolumeFrom { get; set; }
        public int? DailyVolumeTo { get; set; }
        public decimal Amount { get; set; }
        public HashSet<int> Provinces { get; set; } = new();
    }

    #endregion
}
