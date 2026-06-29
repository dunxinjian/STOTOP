using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Amoeba;

/// <summary>
/// 批次5-S5 单票方向化纯函数单测（设计 spec §5.4）。
/// TabToDirection 按 Tab 名定方向；PerUnitValue 按方向选件量分母(出港→发件/进港→派件/公共→合计)。
/// </summary>
public class AmoebaPerUnitTests
{
    [Theory]
    [InlineData("出港", "OUT")]
    [InlineData("出港收入", "OUT")]
    [InlineData("进港", "IN")]
    [InlineData("进港派费", "IN")]
    [InlineData("公共费用", "CMB")]
    [InlineData("管理分摊", "CMB")]
    [InlineData("综合", "CMB")]
    [InlineData(null, "CMB")]
    [InlineData("", "CMB")]
    public void TabToDirection_maps_by_name(string? tabName, string expected)
    {
        Assert.Equal(expected, AmoebaPLService.TabToDirection(tabName));
    }

    [Theory]
    [InlineData("OUT", 1000, 200, 50, 5)]    // 出港：1000 / 发件200
    [InlineData("IN", 600, 200, 50, 12)]     // 进港：600 / 派件50
    [InlineData("CMB", 500, 200, 50, 2)]     // 公共：500 / 合计250
    public void PerUnitValue_divides_by_directional_denominator(
        string direction, decimal amount, decimal send, decimal deliver, decimal expected)
    {
        Assert.Equal(expected, AmoebaPLService.PerUnitValue(direction, amount, send, deliver));
    }

    [Theory]
    [InlineData("OUT", 1000, 0, 50)]    // 发件0 → null
    [InlineData("IN", 600, 200, 0)]     // 派件0 → null
    [InlineData("CMB", 500, 0, 0)]      // 合计0 → null
    public void PerUnitValue_null_when_directional_denominator_zero(
        string direction, decimal amount, decimal send, decimal deliver)
    {
        Assert.Null(AmoebaPLService.PerUnitValue(direction, amount, send, deliver));
    }
}
