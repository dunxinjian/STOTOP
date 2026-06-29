using System.Reflection;
using STOTOP.Module.Express.Models;
using STOTOP.Module.Express.Services.Billing;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

public class PricePlanCacheCityMatchTests
{
    [Fact]
    public void FindCell_prefers_city_cell_before_province_fallback()
    {
        var cache = new PricePlanCache();
        var provinceCell = new PricingCell { ProvinceId = 19, BasePrice = 8m };
        var shenzhenCell = new PricingCell { ProvinceId = 19, CityName = "深圳市", BasePrice = 12m };

        SetField(cache, "_cellIndex", new Dictionary<(long, int, int), PricingCell>
        {
            [(1, 1, 19)] = provinceCell
        });
        SetField(cache, "_cityCellIndex", new Dictionary<(long, int, int, string), PricingCell>
        {
            [(1, 1, 19, "深圳")] = shenzhenCell
        });

        Assert.Same(shenzhenCell, cache.FindCell(1, 1, 19, "深圳市"));
        Assert.Same(provinceCell, cache.FindCell(1, 1, 19, null));
        Assert.Same(provinceCell, cache.FindCell(1, 1, 19, "广州市"));
    }

    private static void SetField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }
}
