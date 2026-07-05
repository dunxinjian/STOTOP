// 申通总部交易明细导入：网点编号 → 经营单元 的 transformRules 表达式回归测试。
// 对应 CardFlowSeeder V66 / 导入规则 3130 的 transformRules（写入 F归属网点编号，供凭证 business_unit 匹配）。
// 映射真源：设计doc 2026-06-19-申通新格式交易明细凭证规则-design.md §账套2 outlet 快照（org192）：
//   320288→城区 / 320319→沙溪 / 321426→浏河 / 321992→南郊。
// 纯 Jint 表达式逻辑，离线可跑（不依赖 SQL Server / ExcelInputPlugin 管道）。

using STOTOP.Module.CardFlow.Services.Import.TransformEngine;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Import;

public class ShentongHqTxTransformTests
{
    // 必须与 CardFlowSeeder 规则 3130 资源(shentong-hqtx-v2-rule3130.json) 的 transformRules 表达式逐字一致（改一处两处同步）。
    private const string Expression =
        "({'320288':'城区','320319':'沙溪','321426':'浏河','321992':'南郊'})[row['F网点编号']] || ''";

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
    [InlineData("320288", "城区")]
    [InlineData("320319", "沙溪")]
    [InlineData("321426", "浏河")]
    [InlineData("321992", "南郊")]
    public void 网点编号映射到经营单元核心名(string code, string expected)
    {
        Assert.Equal(expected, Map(code));
    }

    [Fact]
    public void 未知网点编号映射为空串而非报错()
    {
        Assert.Equal("", Map("9999999"));
    }

    // 核心名须能被 business_unit aux 名(如"城区公司")以 matchBy:contains 命中——"城区公司".Contains("城区")。
    [Theory]
    [InlineData("城区", "城区公司")]
    [InlineData("沙溪", "沙溪公司")]
    [InlineData("浏河", "浏河公司")]
    [InlineData("南郊", "南郊公司")]
    public void 经营单元核心名可被公司名以contains命中(string core, string unitName)
    {
        Assert.Contains(core, unitName);
    }
}
