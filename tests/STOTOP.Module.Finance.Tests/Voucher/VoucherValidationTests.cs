using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Voucher;

/// <summary>
/// 凭证录入校验回归测试：分录数量、借贷平衡、零金额。
/// 这是所有凭证（手工录入与自动记账）入库前的最后一道防线。
/// </summary>
public class VoucherValidationTests
{
    private static CreateVoucherRequest Request(params (decimal Debit, decimal Credit)[] lines)
    {
        var request = new CreateVoucherRequest
        {
            VoucherWord = "记",
            Date = new DateTime(2026, 6, 1),
            PeriodId = 1
        };
        var lineNo = 1;
        foreach (var (debit, credit) in lines)
        {
            request.Entries.Add(new CreateVoucherEntryRequest
            {
                LineNo = lineNo++,
                Summary = "测试分录",
                AccountId = 1001,
                DebitAmount = debit,
                CreditAmount = credit
            });
        }
        return request;
    }

    [Fact]
    public void Balanced_voucher_passes()
    {
        var request = Request((100m, 0m), (0m, 100m));

        var ex = Record.Exception(() => VoucherService.ValidateVoucher(request));

        Assert.Null(ex);
    }

    [Fact]
    public void Multi_line_balanced_voucher_passes()
    {
        var request = Request((60m, 0m), (40m, 0m), (0m, 100m));

        Assert.Null(Record.Exception(() => VoucherService.ValidateVoucher(request)));
    }

    [Fact]
    public void Unbalanced_voucher_throws()
    {
        var request = Request((100m, 0m), (0m, 99.99m));

        var ex = Assert.Throws<InvalidOperationException>(() => VoucherService.ValidateVoucher(request));
        Assert.Contains("借贷不平衡", ex.Message);
    }

    [Fact]
    public void Single_entry_throws()
    {
        var request = Request((100m, 100m));

        var ex = Assert.Throws<InvalidOperationException>(() => VoucherService.ValidateVoucher(request));
        Assert.Contains("至少需要2条分录", ex.Message);
    }

    [Fact]
    public void Empty_entries_throws()
    {
        var request = Request();

        Assert.Throws<InvalidOperationException>(() => VoucherService.ValidateVoucher(request));
    }

    [Fact]
    public void Zero_amount_voucher_throws()
    {
        // 借贷均为 0 虽然"平衡"，但金额为 0 的凭证无意义，必须拒绝
        var request = Request((0m, 0m), (0m, 0m));

        var ex = Assert.Throws<InvalidOperationException>(() => VoucherService.ValidateVoucher(request));
        Assert.Contains("金额不能为0", ex.Message);
    }

    [Fact]
    public void Decimal_precision_is_exact_not_floating()
    {
        // 0.1 + 0.2 必须精确等于 0.3（decimal 语义），不允许浮点误差导致误判
        var request = Request((0.1m, 0m), (0.2m, 0m), (0m, 0.3m));

        Assert.Null(Record.Exception(() => VoucherService.ValidateVoucher(request)));
    }
}
