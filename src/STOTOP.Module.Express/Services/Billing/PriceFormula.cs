using STOTOP.Module.Express.Models;
using STOTOP.Module.Express.Services.PricePlan;

namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 统一运费公式：
/// 1. 合并进位参数（cell覆盖 > 段级默认）
/// 2. 应用进位规则到结算重量
/// 3. 计算：a + CEIL(MAX(0, roundedW - x) / s) * b
/// </summary>
public static class PriceFormula
{
    /// <summary>
    /// 统一运费计算：
    /// 1. 合并进位参数（cell覆盖 > 段级默认）
    /// 2. 应用进位规则到结算重量
    /// 3. 计算：a + CEIL(MAX(0, roundedW - x) / s) * b
    /// 当 ContinuePrice == 0 时退化为固定单价（BasePrice）。
    /// </summary>
    public static decimal Calculate(decimal billableWeight, PricingSegment segment, PricingCell cell)
        => Explain(billableWeight, segment, cell).Amount;

    public static PriceFormulaExplainResult Explain(decimal billableWeight, PricingSegment segment, PricingCell cell)
    {
        var method = cell.RoundingMethodOverride ?? segment.RoundingMethod;
        var trunc = cell.TruncParamOverride ?? segment.TruncParam;
        var ceil = cell.CeilParamOverride ?? segment.CeilParam;
        var roundedWeight = WeightRoundingHelper.RoundWeight(billableWeight, method, trunc, ceil);
        var continueStep = cell.ContinueStep > 0m ? cell.ContinueStep : 1m;

        var result = new PriceFormulaExplainResult
        {
            OriginalWeight = billableWeight,
            RoundedWeight = roundedWeight,
            BasePrice = cell.BasePrice,
            ContinuePrice = cell.ContinuePrice,
            FirstWeight = cell.FirstWeight,
            ContinueStep = continueStep,
            Formula = "base + ceil(max(0, roundedWeight - firstWeight) / continueStep) * continuePrice"
        };

        result.Steps.Add(new PriceFormulaTraceStep
        {
            Step = "round-weight",
            Description = "应用重量进位规则",
            InputValue = billableWeight,
            OutputValue = roundedWeight
        });

        if (cell.ContinuePrice == 0m)
        {
            result.Amount = cell.BasePrice;
            result.Steps.Add(new PriceFormulaTraceStep
            {
                Step = "fixed-price",
                Description = "续重价格为 0，按固定单价计算",
                InputValue = roundedWeight,
                OutputValue = result.Amount
            });
            return result;
        }

        result.ExcessWeight = Math.Max(0m, roundedWeight - cell.FirstWeight);
        result.ContinueUnits = result.ExcessWeight == 0m ? 0m : Math.Ceiling(result.ExcessWeight / continueStep);
        result.Amount = cell.BasePrice + result.ContinueUnits * cell.ContinuePrice;
        result.Steps.Add(new PriceFormulaTraceStep
        {
            Step = "first-continue-weight",
            Description = "首续重公式计算",
            InputValue = roundedWeight,
            OutputValue = result.Amount
        });

        return result;
    }
}

public class PriceFormulaExplainResult
{
    public decimal OriginalWeight { get; set; }
    public decimal RoundedWeight { get; set; }
    public decimal BasePrice { get; set; }
    public decimal ContinuePrice { get; set; }
    public decimal FirstWeight { get; set; }
    public decimal ContinueStep { get; set; }
    public decimal ExcessWeight { get; set; }
    public decimal ContinueUnits { get; set; }
    public decimal Amount { get; set; }
    public string Formula { get; set; } = string.Empty;
    public List<PriceFormulaTraceStep> Steps { get; set; } = [];
}

public class PriceFormulaTraceStep
{
    public string Step { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? InputValue { get; set; }
    public decimal? OutputValue { get; set; }
}
