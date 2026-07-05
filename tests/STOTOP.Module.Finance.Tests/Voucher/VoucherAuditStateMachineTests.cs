using STOTOP.Module.Finance.Entities;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Voucher;

/// <summary>
/// 审核/反审核状态机守卫（缺陷 F3/F5）：
/// 审核仅允许待审核(1)；反审核仅允许已审核(2)；结账锁定(3)/作废(-1) 均不可越权流转。
/// </summary>
public class VoucherAuditStateMachineTests
{
    private const long Org = 100;
    private const long AcctSet = 7;

    private static async Task<long> SeedVoucherAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, int status, string creator = "maker")
    {
        db.Set<FinAccountPeriod>().Add(VoucherServiceTestHarness.Period(11, 2026, 6, AcctSet));
        db.Set<FinVoucher>().Add(new FinVoucher
        {
            FID = 500, FVoucherWord = "记", FVoucherNo = 1, FDate = new DateTime(2026, 6, 10),
            FPeriodId = 11, FStatus = status, FAccountSetId = AcctSet, FOrgId = Org, FCreator = creator
        });
        db.Set<FinVoucherEntry>().AddRange(
            new FinVoucherEntry { FID = 1, FVoucherId = 500, FLineNo = 1, FAccountId = 1, FDebitAmount = 100m, FOrgId = Org },
            new FinVoucherEntry { FID = 2, FVoucherId = 500, FLineNo = 2, FAccountId = 2, FCreditAmount = 100m, FOrgId = Org });
        await db.SaveChangesAsync();
        return 500;
    }

    [Fact]
    public async Task Audit_rejects_locked_voucher()
    {
        await using var db = TestDbContextFactory.Create(nameof(Audit_rejects_locked_voucher), Org);
        var id = await SeedVoucherAsync(db, status: 3); // 结账锁定
        var service = VoucherServiceTestHarness.Build(db, VoucherServiceTestHarness.HttpContext(Org, AcctSet));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuditAsync(id, "auditor"));
        Assert.Contains("反结账", ex.Message);
        Assert.Equal(3, db.Set<FinVoucher>().Single(v => v.FID == id).FStatus); // 状态未变
    }

    [Fact]
    public async Task Audit_rejects_voided_voucher()
    {
        await using var db = TestDbContextFactory.Create(nameof(Audit_rejects_voided_voucher), Org);
        var id = await SeedVoucherAsync(db, status: -1); // 作废
        var service = VoucherServiceTestHarness.Build(db, VoucherServiceTestHarness.HttpContext(Org, AcctSet));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuditAsync(id, "auditor"));
        Assert.Contains("作废", ex.Message);
        Assert.Equal(-1, db.Set<FinVoucher>().Single(v => v.FID == id).FStatus);
    }

    [Fact]
    public async Task Audit_succeeds_on_pending_voucher()
    {
        await using var db = TestDbContextFactory.Create(nameof(Audit_succeeds_on_pending_voucher), Org);
        var id = await SeedVoucherAsync(db, status: 1); // 待审核
        var service = VoucherServiceTestHarness.Build(db, VoucherServiceTestHarness.HttpContext(Org, AcctSet));

        var ok = await service.AuditAsync(id, "auditor");

        Assert.True(ok);
        var v = db.Set<FinVoucher>().Single(x => x.FID == id);
        Assert.Equal(2, v.FStatus);
        Assert.Equal("auditor", v.FAuditor);
    }

    [Fact]
    public async Task Unaudit_rejects_locked_voucher()
    {
        await using var db = TestDbContextFactory.Create(nameof(Unaudit_rejects_locked_voucher), Org);
        var id = await SeedVoucherAsync(db, status: 3); // 结账锁定
        var service = VoucherServiceTestHarness.Build(db, VoucherServiceTestHarness.HttpContext(Org, AcctSet));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UnAuditAsync(id));
        Assert.Contains("反结账", ex.Message);
        Assert.Equal(3, db.Set<FinVoucher>().Single(v => v.FID == id).FStatus);
    }

    [Fact]
    public async Task Unaudit_rejects_draft_voucher()
    {
        await using var db = TestDbContextFactory.Create(nameof(Unaudit_rejects_draft_voucher), Org);
        var id = await SeedVoucherAsync(db, status: 0); // 草稿
        var service = VoucherServiceTestHarness.Build(db, VoucherServiceTestHarness.HttpContext(Org, AcctSet));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UnAuditAsync(id));
        Assert.Contains("已审核", ex.Message);
        Assert.Equal(0, db.Set<FinVoucher>().Single(v => v.FID == id).FStatus);
    }
}
