// 韵达总部交易明细导入：transformRules 表达式回归测试。
// 对应 CardFlowSeeder V65 导入规则 3150（Resources/yunda-hqtx-rule3150.json）的三条 transformRules：
//   ① 符号→双列拆分：F发生额支出=正额、F发生额收入=负额绝对值（凭证规则按列取数、方向固定，规避单列带符号金额坑）；
//   ② 网点编号(F公司编码)→经营单元核心名(城区/浏河) 写入 F归属网点编号（凭证 business_unit 用 matchBy:contains）。
// 纯 Jint 表达式逻辑，离线可跑（不依赖 SQL Server / ExcelInputPlugin 管道）。
// 三条表达式必须与规则 3150 JSON 逐字一致（改一处两处同步）。

using System.Globalization;
using STOTOP.Module.CardFlow.Services.Import.TransformEngine;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Import;

public class YundaHqTxTransformTests
{
    // ↓↓↓ 与 Resources/yunda-hqtx-rule3150.json 的 transformRules[].expression 逐字一致 ↓↓↓
    private const string ExprExpense = "(parseFloat(row['F交易金额'])||0) > 0 ? (parseFloat(row['F交易金额'])||0) : 0";
    private const string ExprIncome = "(parseFloat(row['F交易金额'])||0) < 0 ? -(parseFloat(row['F交易金额'])||0) : 0";
    private const string ExprOutlet = "({'992209':'城区','744706':'浏河'})[row['F公司编码']] || ''";

    private static (decimal income, decimal expense) SplitAmount(string amount)
    {
        var engine = new JintTransformEngine(NullLogger<JintTransformEngine>.Instance);
        var row = new Dictionary<string, string> { ["F交易金额"] = amount };
        var rules = new List<TransformRule>
        {
            new() { TargetColumn = "F发生额支出", Expression = ExprExpense },
            new() { TargetColumn = "F发生额收入", Expression = ExprIncome },
        };
        var r = engine.Execute(row, rules);
        return (Parse(r["F发生额收入"]), Parse(r["F发生额支出"]));
    }

    private static decimal Parse(object? v) =>
        decimal.Parse((v?.ToString() ?? "0"), NumberStyles.Any, CultureInfo.InvariantCulture);

    private static string MapOutlet(string companyCode)
    {
        var engine = new JintTransformEngine(NullLogger<JintTransformEngine>.Instance);
        var rules = new List<TransformRule> { new() { TargetColumn = "F归属网点编号", Expression = ExprOutlet } };
        var row = new Dictionary<string, string> { ["F公司编码"] = companyCode };
        var r = engine.Execute(row, rules);
        return r.TryGetValue("F归属网点编号", out var v) ? v?.ToString() ?? "" : "";
    }

    [Fact]
    public void 扣款正额_落支出列_收入为零()
    {
        var (income, expense) = SplitAmount("289.43"); // 公司扣款凭证=网点被扣=成本/支出
        Assert.Equal(0m, income);
        Assert.Equal(289.43m, expense);
    }

    [Fact]
    public void 退款负额_取绝对值落收入列_支出为零()
    {
        var (income, expense) = SplitAmount("-172.01"); // 公司退款凭证=网点收到=收入
        Assert.Equal(172.01m, income);
        Assert.Equal(0m, expense);
    }

    [Fact]
    public void 零额_两列均为零()
    {
        var (income, expense) = SplitAmount("0");
        Assert.Equal(0m, income);
        Assert.Equal(0m, expense);
    }

    [Theory]
    [InlineData("992209", "城区")]
    [InlineData("744706", "浏河")]
    public void 公司编码映射到经营单元核心名(string code, string expected)
    {
        Assert.Equal(expected, MapOutlet(code));
    }

    [Fact]
    public void 未知公司编码映射为空串而非报错()
    {
        Assert.Equal("", MapOutlet("000000"));
    }
}
