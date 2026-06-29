using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Voucher;

public class VoucherPostingRulesTests
{
    private static List<FinAccountPeriod> Periods() => new()
    {
        new FinAccountPeriod { FID = 10, FAccountSetId = 1, FYear = 2026, FPeriodNo = 5,
            FStartDate = new DateTime(2026, 5, 1), FEndDate = new DateTime(2026, 5, 31), FIsClosed = 1 },
        new FinAccountPeriod { FID = 11, FAccountSetId = 1, FYear = 2026, FPeriodNo = 6,
            FStartDate = new DateTime(2026, 6, 1), FEndDate = new DateTime(2026, 6, 30), FIsClosed = 0 },
        new FinAccountPeriod { FID = 99, FAccountSetId = 2, FYear = 2026, FPeriodNo = 6,
            FStartDate = new DateTime(2026, 6, 1), FEndDate = new DateTime(2026, 6, 30), FIsClosed = 0 },
    };

    [Fact]
    public void ResolvePeriod_returns_matching_period_for_date_and_account_set()
    {
        var p = VoucherPostingRules.ResolvePeriod(Periods(), new DateTime(2026, 6, 15), accountSetId: 1);
        Assert.Equal(11, p.FID);
    }

    [Fact]
    public void ResolvePeriod_does_not_return_period_from_other_account_set()
    {
        // 账套 1 与账套 2 都有 6 月期间（FID 11 vs 99）；用账套 2 查必须返回账套 2 的期间，
        // 真正验证账套隔离（核心安全语义），而非与上一条断言同一次调用。
        var p = VoucherPostingRules.ResolvePeriod(Periods(), new DateTime(2026, 6, 15), accountSetId: 2);
        Assert.Equal(99, p.FID);
        Assert.Equal(2, p.FAccountSetId);
    }

    [Fact]
    public void ResolvePeriod_throws_when_no_period_covers_date()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => VoucherPostingRules.ResolvePeriod(Periods(), new DateTime(2026, 7, 1), accountSetId: 1));
        Assert.Contains("未找到", ex.Message);
    }

    [Fact]
    public void EnsureOpenForPosting_throws_when_period_closed()
    {
        var closed = new FinAccountPeriod { FID = 10, FIsClosed = 1, FYear = 2026, FPeriodNo = 5 };
        var ex = Assert.Throws<InvalidOperationException>(
            () => VoucherPostingRules.EnsureOpenForPosting(closed));
        Assert.Contains("已结账", ex.Message);
    }

    [Fact]
    public void EnsureOpenForPosting_passes_when_period_open()
    {
        var open = new FinAccountPeriod { FID = 11, FIsClosed = 0 };
        Assert.Null(Record.Exception(() => VoucherPostingRules.EnsureOpenForPosting(open)));
    }

    private static FinVoucher OwnedVoucher() => new() { FID = 1, FOrgId = 100, FAccountSetId = 7 };

    [Fact]
    public void IsAccessible_true_when_org_and_account_set_match()
        => Assert.True(VoucherPostingRules.IsAccessible(OwnedVoucher(), currentOrgId: 100, currentAccountSetId: 7));

    [Fact]
    public void IsAccessible_false_when_other_org()
        => Assert.False(VoucherPostingRules.IsAccessible(OwnedVoucher(), currentOrgId: 200, currentAccountSetId: 7));

    [Fact]
    public void IsAccessible_false_when_other_account_set_same_org()
        => Assert.False(VoucherPostingRules.IsAccessible(OwnedVoucher(), currentOrgId: 100, currentAccountSetId: 8));

    [Fact]
    public void IsAccessible_degrades_to_org_only_when_no_account_set_header()
        => Assert.True(VoucherPostingRules.IsAccessible(OwnedVoucher(), currentOrgId: 100, currentAccountSetId: 0));

    [Fact]
    public void IsAccessible_degrades_to_unconstrained_when_no_org_context()
        => Assert.True(VoucherPostingRules.IsAccessible(OwnedVoucher(), currentOrgId: 0, currentAccountSetId: 0));
}
