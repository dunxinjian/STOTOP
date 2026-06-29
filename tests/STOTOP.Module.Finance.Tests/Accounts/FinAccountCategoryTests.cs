using STOTOP.Module.Finance.Constants;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Accounts;

public class FinAccountCategoryTests
{
    [Theory]
    [InlineData("营业收入")]
    [InlineData("营业成本")]
    [InlineData("营业税金及附加")]
    [InlineData("期间费用")]
    [InlineData("其他收益")]
    [InlineData("其他损失")]
    [InlineData("所得税费用")]
    [InlineData("以前年度损益调整")]
    [InlineData("损益")] // 兼容早期以大类名落库的脏数据
    public void IsProfitLoss_returns_true_for_profit_loss_categories(string category)
    {
        Assert.True(FinAccountCategory.IsProfitLoss(category));
    }

    [Theory]
    [InlineData("流动资产")]
    [InlineData("非流动资产")]
    [InlineData("流动负债")]
    [InlineData("所有者权益")]
    [InlineData("成本")]
    [InlineData("")]
    [InlineData(null)]
    public void IsProfitLoss_returns_false_for_non_profit_loss(string? category)
    {
        Assert.False(FinAccountCategory.IsProfitLoss(category));
    }

    [Fact]
    public void ProfitLossCategories_contains_eight_subcategories_plus_umbrella()
    {
        Assert.Equal(9, FinAccountCategory.ProfitLossCategories.Length);
        Assert.Contains("损益", FinAccountCategory.ProfitLossCategories);
        Assert.Contains("营业收入", FinAccountCategory.ProfitLossCategories);
        Assert.Contains("以前年度损益调整", FinAccountCategory.ProfitLossCategories);
    }
}
