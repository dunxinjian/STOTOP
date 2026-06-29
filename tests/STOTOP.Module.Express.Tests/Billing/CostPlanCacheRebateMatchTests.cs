using System.Reflection;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Models;
using STOTOP.Module.Express.Services.Billing;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

/// <summary>
/// 返利标志按名称跨表关联（全局成本项目 FName ←→ 方案成本项 FItemName）。
/// 名称规范化与索引构建抽为纯静态方法，直接覆盖：
///  - 大小写 / 半角空格 / 全角空格(U+3000) / 制表符差异应仍能命中；
///  - 全局项重名应进入诊断（首个生效，后者记为脏数据）；
///  - 匹配上的返利项在计费时金额记为负数。
/// </summary>
public class CostPlanCacheRebateMatchTests
{
    [Theory]
    [InlineData("返利A", "返利A")]            // 基准：无空白
    [InlineData("返利 A", "返利A")]           // 半角空格
    [InlineData("返利　A", "返利A")]          // 全角空格 U+3000
    [InlineData("  返利 A  ", "返利A")]       // 首尾 + 内部空白
    [InlineData("返\tA", "返A")]              // 制表符
    [InlineData("", "")]                       // 空串
    public void NormalizeItemName_trims_and_strips_all_internal_whitespace(string input, string expected)
    {
        Assert.Equal(expected, CostPlanCache.NormalizeItemName(input));
    }

    [Fact]
    public void NormalizeItemName_returns_empty_for_null()
    {
        Assert.Equal(string.Empty, CostPlanCache.NormalizeItemName(null));
    }

    [Fact]
    public void BuildGlobalItemIndex_matches_across_case_and_space_and_records_duplicates()
    {
        var items = new List<ExpCostItem>
        {
            new() { FID = 1, FCode = "REBATE", FName = "返利 A", FIsRebate = true },
            new() { FID = 2, FCode = "FREIGHT", FName = "运费", FIsRebate = false },
            // 与首项规范化后同名（全角空格 + 小写）：脏数据，应记入重名诊断，首个(返利)保留
            new() { FID = 3, FCode = "REBATE_DUP", FName = "返利　a", FIsRebate = false },
        };

        var index = CostPlanCache.BuildGlobalItemIndex(items, out var duplicateNames);

        // 方案项名称"返利　a"(全角空格+小写) 能命中全局项"返利 A"，并取到返利标志
        Assert.True(index.TryGetValue(CostPlanCache.NormalizeItemName("返利　a"), out var rebate));
        Assert.True(rebate!.FIsRebate);
        Assert.Equal(1, rebate.FID); // 首个生效，未被重名项覆盖

        // 普通项正常命中
        Assert.True(index.TryGetValue(CostPlanCache.NormalizeItemName("运费"), out var freight));
        Assert.False(freight!.FIsRebate);

        // 不存在的名称不命中（LoadAsync 据此记为"未匹配方案成本项"）
        Assert.False(index.ContainsKey(CostPlanCache.NormalizeItemName("不存在项")));

        // 重名脏数据被诊断捕获
        Assert.Contains("返利　a", duplicateNames);
    }

    [Fact]
    public void CalcAllCosts_records_matched_rebate_item_as_negative_amount()
    {
        const long planId = 1;
        const int rebateItemId = 9001;
        const int freightItemId = 9002;

        var cache = new CostPlanCache();
        SetPrivateField(cache, "_planIndex", new Dictionary<(long, string), List<CostPlanEntry>>
        {
            [(0, "ST")] = [new CostPlanEntry { PlanId = planId, EffectiveDate = DateTime.MinValue }]
        });
        SetPrivateField(cache, "_itemPeriodIndex", new Dictionary<(long, int), List<CostItemPeriod>>
        {
            [(planId, rebateItemId)] = [NationalPeriod(5m)],
            [(planId, freightItemId)] = [NationalPeriod(10m)]
        });
        // 返利项标记 true，运费项 false
        SetPrivateField(cache, "_rebateIndex", new Dictionary<int, bool>
        {
            [rebateItemId] = true,
            [freightItemId] = false
        });

        var result = cache.CalcAllCosts(0, "ST", 31, null, 1m, new DateTime(2026, 6, 13));

        var rebate = Assert.Single(result.Items, i => i.CostItemId == rebateItemId);
        Assert.True(rebate.IsRebate);
        Assert.Equal(-5m, rebate.Amount); // 返利记为负数

        var freight = Assert.Single(result.Items, i => i.CostItemId == freightItemId);
        Assert.False(freight.IsRebate);
        Assert.Equal(10m, freight.Amount);
    }

    private static CostItemPeriod NationalPeriod(decimal basePrice) => new()
    {
        EffectiveDate = DateTime.MinValue,
        PricingScope = "national",
        Segments =
        [
            new PricingSegment
            {
                SegmentIndex = 0,
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
}
