using System.Collections.Generic;
using STOTOP.Module.Quality.Services.CarrierDashboard;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.CarrierDashboard;

/// <summary>
/// 承运商质控看板跨期合成口径纯函数单测（design spec §3）。
/// WeightedAverage：件量加权，权重和为0退算术平均，全空返回 null。
/// PeriodSum：期间累计，忽略 null。
/// TimeoutTotal：派送超时 T0..T3 求和，null 当 0。
/// </summary>
public class CarrierQualityDashboardSynthesisTests
{
    [Fact]
    public void WeightedAverage_weights_by_volume()
    {
        var items = new List<(decimal?, decimal?)> { (90m, 100m), (80m, 300m) };
        // (90*100 + 80*300) / 400 = 33000/400 = 82.5
        Assert.Equal(82.5m, CarrierQualityDashboardService.WeightedAverage(items));
    }

    [Fact]
    public void WeightedAverage_falls_back_to_arithmetic_when_weights_zero()
    {
        var items = new List<(decimal?, decimal?)> { (90m, 0m), (80m, 0m) };
        Assert.Equal(85m, CarrierQualityDashboardService.WeightedAverage(items));
    }

    [Fact]
    public void WeightedAverage_skips_null_values()
    {
        var items = new List<(decimal?, decimal?)> { (null, 100m), (80m, 200m) };
        Assert.Equal(80m, CarrierQualityDashboardService.WeightedAverage(items));
    }

    [Fact]
    public void WeightedAverage_returns_null_when_no_values()
    {
        var items = new List<(decimal?, decimal?)> { (null, 100m) };
        Assert.Null(CarrierQualityDashboardService.WeightedAverage(items));
    }

    [Fact]
    public void PeriodSum_empty_is_zero()
    {
        var input = new List<decimal?>();
        Assert.Equal(0m, CarrierQualityDashboardService.PeriodSum(input));
    }

    [Fact]
    public void PeriodSum_ignores_null()
    {
        var input = new List<decimal?> { 1m, null, 2m, 3m };
        Assert.Equal(6m, CarrierQualityDashboardService.PeriodSum(input));
    }

    [Theory]
    [InlineData(1, 2, 3, 4, 10)]
    [InlineData(null, null, null, null, 0)]
    [InlineData(5, null, null, null, 5)]
    public void TimeoutTotal_sums_levels_with_null_as_zero(int? t0, int? t1, int? t2, int? t3, int expected)
    {
        Assert.Equal(expected, CarrierQualityDashboardService.TimeoutTotal(t0, t1, t2, t3));
    }
}
