using STOTOP.Module.Express.Models;
using STOTOP.Module.Express.Services.Billing;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

/// <summary>
/// 运费公式回归测试：a + CEIL(MAX(0, roundedW - x) / s) * b。
/// 覆盖边界条件与 cell 级进位参数覆盖段级默认的优先级。
/// </summary>
public class PriceFormulaRegressionTests
{
    private static PricingSegment Segment(int roundingMethod = 1, decimal? trunc = null, decimal? ceil = null)
        => new() { WeightFrom = 0, WeightTo = 100, RoundingMethod = roundingMethod, TruncParam = trunc, CeilParam = ceil };

    private static PricingCell Cell(decimal basePrice, decimal continuePrice, decimal firstWeight = 1m, decimal continueStep = 1m)
        => new() { BasePrice = basePrice, ContinuePrice = continuePrice, FirstWeight = firstWeight, ContinueStep = continueStep };

    [Fact]
    public void Weight_equal_to_first_weight_charges_base_only()
    {
        var result = PriceFormula.Explain(1m, Segment(), Cell(10m, 2m));

        Assert.Equal(10m, result.Amount);
        Assert.Equal(0m, result.ExcessWeight);
        Assert.Equal(0m, result.ContinueUnits);
    }

    [Fact]
    public void Weight_below_first_weight_never_discounts()
    {
        // 超出部分按 MAX(0, ...) 截断，不能出现负的续重单元
        var result = PriceFormula.Explain(0.3m, Segment(), Cell(10m, 2m));

        Assert.Equal(10m, result.Amount);
        Assert.Equal(0m, result.ExcessWeight);
    }

    [Fact]
    public void Fractional_excess_rounds_up_to_full_continue_unit()
    {
        // 1.01kg 超出 0.01 → 仍按 1 个续重单元收费
        var result = PriceFormula.Explain(1.01m, Segment(), Cell(10m, 2m));

        Assert.Equal(12m, result.Amount);
        Assert.Equal(1m, result.ContinueUnits);
    }

    [Fact]
    public void Continue_step_of_half_kg_doubles_units()
    {
        // 超出 2kg / 步长 0.5 = 4 个续重单元
        var result = PriceFormula.Explain(3m, Segment(), Cell(10m, 1.5m, firstWeight: 1m, continueStep: 0.5m));

        Assert.Equal(10m + 4m * 1.5m, result.Amount);
        Assert.Equal(4m, result.ContinueUnits);
    }

    [Fact]
    public void Zero_continue_step_defaults_to_one()
    {
        // 防止除零：步长 <= 0 时按 1 处理
        var result = PriceFormula.Explain(3m, Segment(), Cell(10m, 2m, firstWeight: 1m, continueStep: 0m));

        Assert.Equal(1m, result.ContinueStep);
        Assert.Equal(14m, result.Amount); // 10 + 2单元 * 2
    }

    [Fact]
    public void Cell_rounding_override_takes_precedence_over_segment()
    {
        // 段级=实际重量(1)，cell 覆盖为向上取整(5)：1.2 → 2
        var segment = Segment(roundingMethod: 1);
        var cell = Cell(10m, 2m);
        cell.RoundingMethodOverride = 5;

        var result = PriceFormula.Explain(1.2m, segment, cell);

        Assert.Equal(2m, result.RoundedWeight);
        Assert.Equal(12m, result.Amount); // 10 + 1单元 * 2
    }

    [Fact]
    public void Segment_rounding_applies_when_cell_has_no_override()
    {
        // 段级向上取整(5)：1.2 → 2
        var result = PriceFormula.Explain(1.2m, Segment(roundingMethod: 5), Cell(10m, 2m));

        Assert.Equal(2m, result.RoundedWeight);
        Assert.Equal(12m, result.Amount);
    }

    [Fact]
    public void Explain_trace_contains_round_weight_step()
    {
        var result = PriceFormula.Explain(2.5m, Segment(), Cell(10m, 2m));

        Assert.Contains(result.Steps, s => s.Step == "round-weight");
        Assert.Contains(result.Steps, s => s.Step == "first-continue-weight");
        Assert.Equal(result.Amount, PriceFormula.Calculate(2.5m, Segment(), Cell(10m, 2m)));
    }
}
