using STOTOP.Module.Express.Models;
using STOTOP.Module.Express.Services.Billing;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

public class PriceFormulaExplainTests
{
    [Fact]
    public void Explain_returns_fixed_price_when_continue_price_is_zero()
    {
        var segment = new PricingSegment { WeightFrom = 0, WeightTo = 10, RoundingMethod = 0 };
        var cell = new PricingCell { BasePrice = 8m, ContinuePrice = 0m, FirstWeight = 1m, ContinueStep = 1m };

        var result = PriceFormula.Explain(3.2m, segment, cell);

        Assert.Equal(8m, result.Amount);
        Assert.Equal(3.2m, result.RoundedWeight);
        Assert.Contains(result.Steps, s => s.Step == "fixed-price");
    }

    [Fact]
    public void Explain_returns_first_continue_weight_steps()
    {
        var segment = new PricingSegment { WeightFrom = 0, WeightTo = 10, RoundingMethod = 0 };
        var cell = new PricingCell { BasePrice = 10m, ContinuePrice = 2m, FirstWeight = 1m, ContinueStep = 1m };

        var result = PriceFormula.Explain(3.2m, segment, cell);

        Assert.Equal(16m, result.Amount);
        Assert.Equal(3.2m, result.RoundedWeight);
        Assert.Equal(2.2m, result.ExcessWeight);
        Assert.Equal(3m, result.ContinueUnits);
        Assert.Contains("base + ceil", result.Formula);
    }
}
