using STOTOP.Module.Quality.Services.Unification;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Unify;

/// <summary>
/// ParseUtil.TryDecimal 的纯函数级护栏（脱库快速跑）。
/// 重点：率字段尾缀口径——以「百分数数值」存（不除 100）；万分号 ‱ ×0.01、千分号 ‰ ×0.1、百分号 % ×1。
/// 申通「积压异常监控」真源含 6.4‱ / 3.69‱（进港/虚签投诉率），曾被 decimal.TryParse 判 false 落 null 丢数据。
/// </summary>
public class ParseUtilTests
{
    [Theory]
    // 万分号 ‱ (U+2031)：6.4‱ = 0.064%（百分数值）→ 0.064；3.69‱ → 0.0369
    [InlineData("6.4‱", 0.064)]
    [InlineData("3.69‱", 0.0369)]
    // 千分号 ‰ (U+2030)：5‰ = 0.5% → 0.5
    [InlineData("5‰", 0.5)]
    // 百分号 %：100.00% → 100.00（只去符号、不除 100）
    [InlineData("100.00%", 100.00)]
    // 纯数值（无尾缀，如积压倍数/遗失率ppm）原样解析，不受影响
    [InlineData("0.01", 0.01)]
    [InlineData("0.0", 0.0)]
    [InlineData("  6.4‱  ", 0.064)] // 前后空白容错
    public void TryDecimal_ParsesPermilleAndPercent(string input, double expected)
    {
        var actual = ParseUtil.TryDecimal(input);
        Assert.NotNull(actual);
        // 容差比较，避免浮点/精度脆弱
        Assert.True(global::System.Math.Abs(actual!.Value - (decimal)expected) < 0.00001m,
            $"输入 '{input}' 期望 ≈{expected}，实际 {actual}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("--")]
    public void TryDecimal_ReturnsNull_OnEmptyOrInvalid(string? input)
    {
        Assert.Null(ParseUtil.TryDecimal(input));
    }
}
