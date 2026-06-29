using STOTOP.Module.Express.Services.Billing;
using STOTOP.Module.Express.Models;
using System.Reflection;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

public class CostPlanCacheExplainTests
{
    [Fact]
    public void ExplainAllCosts_returns_configuration_issue_when_plan_missing()
    {
        var cache = new CostPlanCache();

        var result = cache.ExplainAllCosts(
            networkPointId: 1001,
            brandCode: "STO",
            provinceId: 31,
            cityName: "上海市",
            billableWeight: 2.5m,
            waybillDate: new DateTime(2026, 6, 10));

        Assert.Null(result.PlanId);
        Assert.Contains(result.ConfigurationIssues, x => x.Contains("未命中成本方案"));
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData("楚雄", "楚雄彝族自治州")]
    [InlineData("大理", "大理白族自治州")]
    [InlineData("昆明", "昆明市")]
    [InlineData("德宏", "德宏傣族景颇族自治州")]
    [InlineData("红河", "红河哈尼族彝族自治州")]
    public void ExplainAllCosts_matches_city_surcharge_by_simplified_city_keyword(
        string waybillCityName,
        string matrixCityName)
    {
        var cache = CreateCacheWithCitySurcharge(matrixCityName);

        var result = cache.ExplainAllCosts(
            networkPointId: 0,
            brandCode: "ST",
            provinceId: 25,
            cityName: waybillCityName,
            billableWeight: 1m,
            waybillDate: new DateTime(2026, 6, 11));

        var item = Assert.Single(result.Items);
        Assert.Equal(99, item.CostItemId);
        Assert.Equal(7m, item.Amount);
    }

    private static CostPlanCache CreateCacheWithCitySurcharge(string matrixCityName)
    {
        var cache = new CostPlanCache();
        const long planId = 1;
        const int costItemId = 99;

        SetPrivateField(cache, "_planIndex", new Dictionary<(long, string), List<CostPlanEntry>>
        {
            [(0, "ST")] = [new CostPlanEntry { PlanId = planId, EffectiveDate = DateTime.MinValue }]
        });
        SetPrivateField(cache, "_itemPeriodIndex", new Dictionary<(long, int), List<CostItemPeriod>>
        {
            [(planId, costItemId)] =
            [
                new CostItemPeriod
                {
                    EffectiveDate = DateTime.MinValue,
                    PricingScope = "city",
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
                                    ProvinceId = 25,
                                    CityId = 2078,
                                    CityName = matrixCityName,
                                    BasePrice = 7m,
                                    ContinuePrice = 0m,
                                    FirstWeight = 0m,
                                    ContinueStep = 1m
                                },
                                new PricingCell
                                {
                                    ProvinceId = 25,
                                    BasePrice = 2m,
                                    ContinuePrice = 0m,
                                    FirstWeight = 0m,
                                    ContinueStep = 1m
                                }
                            ]
                        }
                    ]
                }
            ]
        });
        SetPrivateField(cache, "_itemOutletIndex", new Dictionary<(long, int), HashSet<long>>
        {
            [(planId, costItemId)] = []
        });
        SetPrivateField(cache, "_rebateIndex", new Dictionary<int, bool>
        {
            [costItemId] = false
        });

        return cache;
    }

    private static void SetPrivateField<T>(CostPlanCache cache, string name, T value)
    {
        var field = typeof(CostPlanCache).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field {name}");
        field.SetValue(cache, value);
    }
}
