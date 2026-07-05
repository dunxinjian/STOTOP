using System.Reflection;
using STOTOP.Module.Express.Models;
using STOTOP.Module.Express.Services.Billing;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

/// <summary>
/// 一口价互斥：店铺命中一口价项 → 一口价 + 未被互斥规则排除的项叠加（CostMode=2）；
/// 未命中 → 标准项叠加且跳过全部一口价项（CostMode=1）。
/// </summary>
public class CostPlanCacheFixedPriceTests
{
    private const long PlanId = 1;
    private const int FixedPriceItemId = 11;   // 一口价项（关联店铺：一口价店铺A）
    private const int LabelItemId = 13;        // 面单服务费（被互斥规则排除）
    private const int DispatchItemId = 14;     // 出港派费（被互斥规则排除）
    private const int AddonItemId = 25;        // 周期性加收（未被排除，一口价下仍叠加）

    private static readonly DateTime WaybillDate = new(2026, 6, 1);

    private static CostPlanCache CreateCache(
        bool fixedPriceHasShops = true,
        bool withExclusion = true,
        bool fixedPriceHasPeriod = true)
    {
        var cache = new CostPlanCache();

        SetPrivateField(cache, "_planIndex", new Dictionary<(long, string), List<CostPlanEntry>>
        {
            [(0, "ST")] = [new CostPlanEntry { PlanId = PlanId, EffectiveDate = DateTime.MinValue }]
        });

        var periodIndex = new Dictionary<(long, int), List<CostItemPeriod>>
        {
            [(PlanId, LabelItemId)] = [NationalPeriod(0.91m)],
            [(PlanId, DispatchItemId)] = [NationalPeriod(2.00m)],
            [(PlanId, AddonItemId)] = [NationalPeriod(0.30m)]
        };
        if (fixedPriceHasPeriod)
            periodIndex[(PlanId, FixedPriceItemId)] = [NationalPeriod(1.58m)];
        SetPrivateField(cache, "_itemPeriodIndex", periodIndex);

        var fixedEntry = new FixedPriceItemEntry { ItemId = FixedPriceItemId };
        if (fixedPriceHasShops)
            fixedEntry.ShopNames.Add("一口价店铺A");

        SetPrivateField(cache, "_fixedPriceIndex", new Dictionary<long, List<FixedPriceItemEntry>>
        {
            [PlanId] = [fixedEntry]
        });
        SetPrivateField(cache, "_fixedPriceItemIds", new HashSet<int> { FixedPriceItemId });

        if (withExclusion)
        {
            SetPrivateField(cache, "_exclusionIndex",
                new Dictionary<long, List<(DateTime EffectiveDate, HashSet<int> ExcludedItemIds)>>
                {
                    [PlanId] = [(DateTime.MinValue, new HashSet<int> { LabelItemId, DispatchItemId })]
                });
        }

        return cache;
    }

    private static CostItemPeriod NationalPeriod(decimal basePrice) => new()
    {
        EffectiveDate = DateTime.MinValue,
        PricingScope = "national",
        Segments =
        [
            new PricingSegment
            {
                SegmentIndex = 1,
                WeightFrom = 0m,
                WeightTo = null,
                Cells =
                [
                    new PricingCell
                    {
                        ProvinceId = 0,
                        BasePrice = basePrice,
                        ContinuePrice = 0m,
                        FirstWeight = 0m,
                        ContinueStep = 1m
                    }
                ]
            }
        ]
    };

    private static void SetPrivateField(CostPlanCache cache, string fieldName, object value)
    {
        var field = typeof(CostPlanCache).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(cache, value);
    }

    private static CostCalcResult Calc(CostPlanCache cache, string? shopName)
        => cache.CalcAllCosts(0, "ST", 31, null, 1m, WaybillDate, shopName);

    [Fact]
    public void Standard_mode_excludes_fixed_price_items()
    {
        var cache = CreateCache();
        var result = Calc(cache, shopName: "普通店铺");

        Assert.Equal(1, result.CostMode);
        Assert.Null(result.FixedPriceItemId);
        Assert.DoesNotContain(result.Items, i => i.CostItemId == FixedPriceItemId);
        // 标准模式：面单 + 出港派费 + 周期加收 全部叠加
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(0.91m + 2.00m + 0.30m, result.Items.Sum(i => i.Amount));
    }

    [Fact]
    public void Fixed_price_mode_applies_exclusion_rule()
    {
        var cache = CreateCache();
        var result = Calc(cache, shopName: "一口价店铺A");

        Assert.Equal(2, result.CostMode);
        Assert.Equal(FixedPriceItemId, result.FixedPriceItemId);
        // 一口价 1.58 + 未被排除的周期加收 0.30；面单/出港派费被互斥规则排除
        Assert.Contains(result.Items, i => i.CostItemId == FixedPriceItemId && i.Amount == 1.58m);
        Assert.Contains(result.Items, i => i.CostItemId == AddonItemId && i.Amount == 0.30m);
        Assert.DoesNotContain(result.Items, i => i.CostItemId == LabelItemId);
        Assert.DoesNotContain(result.Items, i => i.CostItemId == DispatchItemId);
        Assert.Equal(1.58m + 0.30m, result.Items.Sum(i => i.Amount));
    }

    [Fact]
    public void Fixed_price_mode_without_exclusion_rule_keeps_other_items()
    {
        var cache = CreateCache(withExclusion: false);
        var result = Calc(cache, shopName: "一口价店铺A");

        Assert.Equal(2, result.CostMode);
        // 未配置互斥规则：一口价 + 全部标准项叠加（规则缺失时不隐式排除）
        Assert.Equal(4, result.Items.Count);
    }

    [Fact]
    public void Fixed_price_item_without_shops_is_inactive()
    {
        var cache = CreateCache(fixedPriceHasShops: false);
        var result = Calc(cache, shopName: "一口价店铺A");

        // 空店铺=不生效：走标准模式，且一口价项不参与
        Assert.Equal(1, result.CostMode);
        Assert.DoesNotContain(result.Items, i => i.CostItemId == FixedPriceItemId);
    }

    [Fact]
    public void Null_shop_name_falls_back_to_standard_mode()
    {
        var cache = CreateCache();
        var result = Calc(cache, shopName: null);

        Assert.Equal(1, result.CostMode);
        Assert.DoesNotContain(result.Items, i => i.CostItemId == FixedPriceItemId);
    }

    [Fact]
    public void Explain_reports_fixed_price_mode_and_exclusions()
    {
        var cache = CreateCache();
        var result = cache.ExplainAllCosts(0, "ST", 31, null, 1m, WaybillDate, "一口价店铺A");

        Assert.Equal(2, result.CostMode);
        Assert.Equal(FixedPriceItemId, result.FixedPriceItemId);
        Assert.Contains(result.MatchNotes, n => n.Contains("命中一口价"));
        Assert.Contains(result.MatchNotes, n => n.Contains("互斥规则生效"));
        Assert.Equal(1.58m + 0.30m, result.TotalAmount);
    }

    // === P0：一口价命中但算不出主成本时不得静默缩水（回退标准模式或判失败）===

    [Fact]
    public void Fixed_price_item_without_effective_period_falls_back_to_standard_mode()
    {
        // 命中店铺但一口价项在运单日期无生效期间 → 不进入一口价模式，避免互斥掉标准项后主成本蒸发
        var cache = CreateCache(fixedPriceHasPeriod: false);
        var result = Calc(cache, shopName: "一口价店铺A");

        Assert.Equal(1, result.CostMode);
        Assert.False(result.FixedPriceUnresolved);
        Assert.DoesNotContain(result.Items, i => i.CostItemId == FixedPriceItemId);
        // 标准模式：互斥规则不生效，标准项照常叠加
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(0.91m + 2.00m + 0.30m, result.Items.Sum(i => i.Amount));
    }

    [Fact]
    public void Fixed_price_item_restricted_to_other_network_falls_back_to_standard_mode()
    {
        // 命中店铺但一口价项限定到别的网点 → 不进入一口价模式
        var cache = CreateCache();
        SetPrivateField(cache, "_itemOutletIndex", new Dictionary<(long, int), HashSet<long>>
        {
            [(PlanId, FixedPriceItemId)] = new HashSet<long> { 999L } // 仅网点999适用；运单网点=0 不在其中
        });

        var result = Calc(cache, shopName: "一口价店铺A");

        Assert.Equal(1, result.CostMode);
        Assert.False(result.FixedPriceUnresolved);
        Assert.DoesNotContain(result.Items, i => i.CostItemId == FixedPriceItemId);
    }

    [Fact]
    public void Fixed_price_mode_with_missing_price_cell_is_marked_unresolved()
    {
        // 一口价项有生效期间且网点适用，但目的地价格单元格缺失 → 算不出金额，
        // 标记 FixedPriceUnresolved，交由引擎判为失败而非写入仅剩加收项的缩水成本
        var cache = new CostPlanCache();
        SetPrivateField(cache, "_planIndex", new Dictionary<(long, string), List<CostPlanEntry>>
        {
            [(0, "ST")] = [new CostPlanEntry { PlanId = PlanId, EffectiveDate = DateTime.MinValue }]
        });
        SetPrivateField(cache, "_itemPeriodIndex", new Dictionary<(long, int), List<CostItemPeriod>>
        {
            // 一口价项为省份矩阵，但只配了省份99的单元格，无省份31、无全国回退 → 命中不了
            [(PlanId, FixedPriceItemId)] = [ProvinceOnlyPeriod(99, 1.58m)],
            [(PlanId, AddonItemId)] = [NationalPeriod(0.30m)]
        });
        var fixedEntry = new FixedPriceItemEntry { ItemId = FixedPriceItemId };
        fixedEntry.ShopNames.Add("一口价店铺A");
        SetPrivateField(cache, "_fixedPriceIndex", new Dictionary<long, List<FixedPriceItemEntry>>
        {
            [PlanId] = [fixedEntry]
        });
        SetPrivateField(cache, "_fixedPriceItemIds", new HashSet<int> { FixedPriceItemId });

        var result = cache.CalcAllCosts(0, "ST", 31, null, 1m, WaybillDate, "一口价店铺A");

        Assert.Equal(2, result.CostMode);
        Assert.True(result.FixedPriceUnresolved);
        Assert.DoesNotContain(result.Items, i => i.CostItemId == FixedPriceItemId);
    }

    private static CostItemPeriod ProvinceOnlyPeriod(int provinceId, decimal basePrice) => new()
    {
        EffectiveDate = DateTime.MinValue,
        PricingScope = "province",
        Segments =
        [
            new PricingSegment
            {
                SegmentIndex = 1,
                WeightFrom = 0m,
                WeightTo = null,
                Cells =
                [
                    new PricingCell
                    {
                        ProvinceId = provinceId,
                        BasePrice = basePrice,
                        ContinuePrice = 0m,
                        FirstWeight = 0m,
                        ContinueStep = 1m
                    }
                ]
            }
        ]
    };
}
