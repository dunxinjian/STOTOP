using System.Reflection;
using STOTOP.Module.Express.Models;
using STOTOP.Module.Express.Services.Billing;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

/// <summary>
/// P1 #18：成本项时间段失效日期。非空失效日期后的运单不再命中本期间（不再静默沿用旧月价格）；
/// 失效日期为空时无上限（沿用旧行为）。
/// </summary>
public class CostPlanCachePeriodExpiryTests
{
    private const long PlanId = 1;
    private const int ItemId = 50;

    [Fact]
    public void Waybill_after_expiry_does_not_apply_stale_price()
    {
        var cache = BuildCache(effective: new DateTime(2026, 4, 1), expiry: new DateTime(2026, 4, 30));

        var result = cache.CalcAllCosts(0, "ST", 31, null, 1m, new DateTime(2026, 5, 15), null);

        // 4月期间已失效，5月运单不再取用其价格（避免缺5月价时静默沿用4月价）
        Assert.DoesNotContain(result.Items, i => i.CostItemId == ItemId);
    }

    [Fact]
    public void Waybill_within_expiry_applies()
    {
        var cache = BuildCache(effective: new DateTime(2026, 4, 1), expiry: new DateTime(2026, 4, 30));

        var result = cache.CalcAllCosts(0, "ST", 31, null, 1m, new DateTime(2026, 4, 15), null);

        Assert.Contains(result.Items, i => i.CostItemId == ItemId && i.Amount == 1.00m);
    }

    [Fact]
    public void Waybill_on_expiry_date_still_applies()
    {
        var cache = BuildCache(effective: new DateTime(2026, 4, 1), expiry: new DateTime(2026, 4, 30));

        // 失效日期当日（含当日）仍命中
        var result = cache.CalcAllCosts(0, "ST", 31, null, 1m, new DateTime(2026, 4, 30), null);

        Assert.Contains(result.Items, i => i.CostItemId == ItemId);
    }

    [Fact]
    public void Null_expiry_applies_to_far_future_waybill()
    {
        var cache = BuildCache(effective: new DateTime(2026, 4, 1), expiry: null);

        var result = cache.CalcAllCosts(0, "ST", 31, null, 1m, new DateTime(2027, 1, 1), null);

        Assert.Contains(result.Items, i => i.CostItemId == ItemId);
    }

    private static CostPlanCache BuildCache(DateTime effective, DateTime? expiry)
    {
        var cache = new CostPlanCache();
        SetPrivateField(cache, "_planIndex", new Dictionary<(long, string), List<CostPlanEntry>>
        {
            [(0, "ST")] = [new CostPlanEntry { PlanId = PlanId, EffectiveDate = DateTime.MinValue }]
        });
        SetPrivateField(cache, "_itemPeriodIndex", new Dictionary<(long, int), List<CostItemPeriod>>
        {
            [(PlanId, ItemId)] =
            [
                new CostItemPeriod
                {
                    EffectiveDate = effective,
                    ExpiryDate = expiry,
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
                                    BasePrice = 1.00m,
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
        return cache;
    }

    private static void SetPrivateField(CostPlanCache cache, string fieldName, object value)
    {
        var field = typeof(CostPlanCache).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(cache, value);
    }
}
