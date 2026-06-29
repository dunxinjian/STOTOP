using STOTOP.Module.Express.Services.PricePlan;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

/// <summary>
/// 重量进位算法回归测试（7种进位方式）。
/// 进位是计费金额的第一步，任何行为变化都会直接影响应收金额。
/// </summary>
public class WeightRoundingHelperTests
{
    // 方式1：实际重量，原样返回
    [Theory]
    [InlineData(0.01, 0.01)]
    [InlineData(3.27, 3.27)]
    [InlineData(10, 10)]
    public void Method1_actual_weight_passes_through(decimal input, decimal expected)
        => Assert.Equal(expected, WeightRoundingHelper.RoundWeight(input, 1, null, null));

    // 方式2：四舍五入（AwayFromZero，0.5 必须进位）
    [Theory]
    [InlineData(1.4, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2.5, 3)] // AwayFromZero：不允许银行家舍入到 2
    [InlineData(1.49, 1)]
    public void Method2_rounds_half_away_from_zero(decimal input, decimal expected)
        => Assert.Equal(expected, WeightRoundingHelper.RoundWeight(input, 2, null, null));

    // 方式3：点五进位（小数 >= 0.5 向上，否则向下）
    [Theory]
    [InlineData(1.49, 1)]
    [InlineData(1.5, 2)]
    [InlineData(1.51, 2)]
    [InlineData(2.0, 2)]
    public void Method3_half_kg_rounding(decimal input, decimal expected)
        => Assert.Equal(expected, WeightRoundingHelper.RoundWeight(input, 3, null, null));

    // 方式4：按阈值进舍；阈值 NULL/<=0 时向下取整（兼容旧逻辑）
    [Theory]
    [InlineData(1.3, 0.3, 2)]  // 小数 0.3 >= 阈值 0.3 → 进位
    [InlineData(1.29, 0.3, 1)] // 小数 0.29 < 阈值 → 舍去
    [InlineData(1.99, null, 1)] // 阈值 NULL → 向下取整
    [InlineData(1.99, 0.0, 1)]  // 阈值 0 → 向下取整
    public void Method4_threshold_rounding(decimal input, double? ceilParam, decimal expected)
        => Assert.Equal(expected, WeightRoundingHelper.RoundWeight(input, 4, null, (decimal?)ceilParam));

    // 方式5：向上取整
    [Theory]
    [InlineData(1.01, 2)]
    [InlineData(1.0, 1)]
    [InlineData(0.1, 1)]
    public void Method5_ceiling(decimal input, decimal expected)
        => Assert.Equal(expected, WeightRoundingHelper.RoundWeight(input, 5, null, null));

    // 方式6：分段进位（步进值模式）
    [Theory]
    [InlineData(1.1, 0.5, 1.5)]
    [InlineData(1.6, 0.5, 2.0)]
    [InlineData(1.5, 0.5, 1.5)] // 恰好整步进不再进位
    public void Method6_step_rounding(decimal input, decimal ceilParam, decimal expected)
        => Assert.Equal(expected, WeightRoundingHelper.RoundWeight(input, 6, null, ceilParam));

    // 方式6：步进值缺失时回退旧分段逻辑（0-1kg按0.1 / 1-10kg按0.5 / 10kg+按1）
    [Theory]
    [InlineData(0.23, 0.3)]
    [InlineData(1.1, 1.5)]
    [InlineData(9.6, 10)]
    [InlineData(10.2, 11)]
    public void Method6_legacy_segment_fallback(decimal input, decimal expected)
        => Assert.Equal(expected, WeightRoundingHelper.RoundWeight(input, 6, null, null));

    // 方式7：进位到基数倍数（默认100）
    [Theory]
    [InlineData(99, null, 100)]
    [InlineData(150, null, 200)]
    [InlineData(100, null, 100)]
    [InlineData(120, 50.0, 150)]
    public void Method7_round_to_base_multiple(decimal input, double? ceilParam, decimal expected)
        => Assert.Equal(expected, WeightRoundingHelper.RoundWeight(input, 7, null, (decimal?)ceilParam));

    // 未知方式：原样返回，不抛异常
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void Unknown_method_passes_through(int method)
        => Assert.Equal(3.21m, WeightRoundingHelper.RoundWeight(3.21m, method, null, null));
}
