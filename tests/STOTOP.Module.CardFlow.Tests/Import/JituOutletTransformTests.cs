// 极兔总部交易明细导入：网点编号 → 经营单元 的 transformRules 表达式回归测试。
// 对应 CardFlowSeeder V64 导入规则 3140 的 transformRules[0]（写入 F归属网点编号，供凭证 business_unit 匹配）。
// 重点覆盖「陆渡=浏河」特例：极兔陆渡网点(3512906) 的经营单元是「浏河」，无法靠 keyword/contains 从网点名自动得出，必须显式映射。
// 纯 Jint 表达式逻辑，离线可跑（不依赖 SQL Server / ExcelInputPlugin 管道）。

using STOTOP.Module.CardFlow.Services.Import.TransformEngine;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Import;

public class JituOutletTransformTests
{
    // 必须与 CardFlowSeeder 规则 3140 的 transformRules[0].expression 逐字一致（改一处两处同步）。
    private const string Expression =
        "({'3512907':'南郊','3512894':'城区','3512906':'浏河'})[row['F网点编号']] || ''";

    private static string Map(string networkCode)
    {
        var engine = new JintTransformEngine(NullLogger<JintTransformEngine>.Instance);
        var rules = new List<TransformRule>
        {
            new() { TargetColumn = "F归属网点编号", Expression = Expression },
        };
        var row = new Dictionary<string, string> { ["F网点编号"] = networkCode };
        var result = engine.Execute(row, rules);
        return result.TryGetValue("F归属网点编号", out var v) ? v?.ToString() ?? "" : "";
    }

    [Theory]
    [InlineData("3512907", "南郊")]
    [InlineData("3512894", "城区")]
    [InlineData("3512906", "浏河")] // 陆渡=浏河 特例：极兔叫陆渡，经营单元是浏河
    public void 网点编号映射到经营单元核心名(string code, string expected)
    {
        Assert.Equal(expected, Map(code));
    }

    [Fact]
    public void 未知网点编号映射为空串而非报错()
    {
        Assert.Equal("", Map("9999999"));
    }
}
