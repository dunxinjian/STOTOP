using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Accounts;

public class InitialBalanceValidatorTests
{
    private static IReadOnlyDictionary<long, (decimal Debit, decimal Credit)> Map(params (long id, decimal d, decimal c)[] rows)
        => rows.ToDictionary(r => r.id, r => (r.d, r.c));

    [Fact]
    public void Balanced_when_total_debit_equals_total_credit()
    {
        var (debit, credit) = InitialBalanceValidator.ComputeTotals(
            new long[] { 1, 2 }, Map(), Map((1, 1000m, 0m), (2, 0m, 1000m)));
        Assert.Equal(1000m, debit);
        Assert.Equal(1000m, credit);
        Assert.True(InitialBalanceValidator.IsBalanced(debit - credit));
    }

    [Fact]
    public void Unbalanced_when_debit_exceeds_credit()
    {
        var (debit, credit) = InitialBalanceValidator.ComputeTotals(
            new long[] { 1, 2 }, Map(), Map((1, 1000m, 0m), (2, 0m, 800m)));
        Assert.Equal(200m, debit - credit);
        Assert.False(InitialBalanceValidator.IsBalanced(debit - credit));
    }

    [Fact]
    public void Submitted_value_overrides_existing_for_same_account()
    {
        // 库中旧值平（999/999），本次只改科目1借方为1000 → 1000-999=1 不平
        var (debit, credit) = InitialBalanceValidator.ComputeTotals(
            new long[] { 1, 2 },
            Map((1, 999m, 0m), (2, 0m, 999m)),
            Map((1, 1000m, 0m)));
        Assert.Equal(1m, debit - credit);
    }

    [Fact]
    public void Unsubmitted_leaf_uses_existing_value()
    {
        // 科目2未提交，取库中贷500；科目1提交借500 → 平
        var (debit, credit) = InitialBalanceValidator.ComputeTotals(
            new long[] { 1, 2 },
            Map((2, 0m, 500m)),
            Map((1, 500m, 0m)));
        Assert.Equal(0m, debit - credit);
        Assert.True(InitialBalanceValidator.IsBalanced(debit - credit));
    }

    [Theory]
    [InlineData(0.004, true)]
    [InlineData(0.006, false)]
    public void Tolerance_boundary(double diff, bool expectedBalanced)
    {
        Assert.Equal(expectedBalanced, InitialBalanceValidator.IsBalanced((decimal)diff));
    }

    [Fact]
    public void Empty_is_balanced()
    {
        var (debit, credit) = InitialBalanceValidator.ComputeTotals(new long[] { 1, 2 }, Map(), Map());
        Assert.Equal(0m, debit);
        Assert.Equal(0m, credit);
        Assert.True(InitialBalanceValidator.IsBalanced(debit - credit));
    }
}
