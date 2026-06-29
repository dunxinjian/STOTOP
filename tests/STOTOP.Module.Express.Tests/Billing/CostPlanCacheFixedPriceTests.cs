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
        bool withExclusion = true)
    {
        var cache = new CostPlanCache();

        SetPrivateField(cache, "_planIndex", new Dictionary<(long, string), List<CostPlanEntry>>
        {
            [(0, "ST")] = [new CostPlanEntry { PlanId = PlanId, EffectiveDate = DateTime.MinValue }]
        });

        SetPrivateField(cache, "_itemPeriodIndex", new Dictionary<(long, int), List<CostItemPeriod>>
        {
            [(PlanId, FixedPriceItemId)] = [NationalPeriod(1.58m)],
            [(PlanId, LabelItemId)] = [NationalPeriod(0.91m)],
            [(PlanId, DispatchItemId)] = [NationalPeriod(2.00m)],
            [(PlanId, AddonItemId)] = [NationalPeriod(0.30m)]
        });

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
}
