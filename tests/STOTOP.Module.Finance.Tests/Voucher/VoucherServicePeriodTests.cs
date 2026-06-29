using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Voucher;

public class VoucherServicePeriodTests
{
    private const long Org = 100;
    private const long AcctSet = 7;

    private static CreateVoucherRequest BalancedRequest(DateTime date) => new()
    {
        VoucherWord = "记",
        Date = date,
        PeriodId = 0, // 模拟前端不传期间
        Entries =
        {
            new CreateVoucherEntryRequest { LineNo = 1, Summary = "测试", AccountId = 1, DebitAmount = 100m },
            new CreateVoucherEntryRequest { LineNo = 2, Summary = "测试", AccountId = 2, CreditAmount = 100m },
        }
    };

    private static async Task SeedAsync(STOTOP.Infrastructure.Data.STOTOPDbContext db, int periodNo, int isClosed = 0)
    {
        db.Set<FinAccount>().AddRange(
            VoucherServiceTestHarness.Account(1, "1001", "库存现金", AcctSet, Org),
            VoucherServiceTestHarness.Account(2, "3001", "实收资本", AcctSet, Org));
        db.Set<FinAccountPeriod>().Add(
            VoucherServiceTestHarness.Period(11, 2026, periodNo, AcctSet, isClosed));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_resolves_period_and_account_set_from_date_when_periodId_zero()
    {
        await using var db = TestDbContextFactory.Create(nameof(Create_resolves_period_and_account_set_from_date_when_periodId_zero), Org);
        await SeedAsync(db, periodNo: 6);
        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var service = VoucherServiceTestHarness.Build(db, http);

        var dto = await service.CreateAsync(BalancedRequest(new DateTime(2026, 6, 15)), "tester", AcctSet);

        var saved = db.Set<FinVoucher>().Single(v => v.FID == dto.Id);
        Assert.Equal(11, saved.FPeriodId);
        Assert.Equal(AcctSet, saved.FAccountSetId);
        Assert.Equal(Org, saved.FOrgId);
    }

    [Fact]
    public async Task Create_rejects_when_no_period_for_date()
    {
        await using var db = TestDbContextFactory.Create(nameof(Create_rejects_when_no_period_for_date), Org);
        await SeedAsync(db, periodNo: 6);
        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var service = VoucherServiceTestHarness.Build(db, http);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(BalancedRequest(new DateTime(2026, 7, 1)), "tester", AcctSet));
        Assert.Contains("未找到", ex.Message);
        Assert.Empty(db.Set<FinVoucher>());
    }

    [Fact]
    public async Task Create_rejects_when_target_period_closed()
    {
        await using var db = TestDbContextFactory.Create(nameof(Create_rejects_when_target_period_closed), Org);
        await SeedAsync(db, periodNo: 6, isClosed: 1);
        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var service = VoucherServiceTestHarness.Build(db, http);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(BalancedRequest(new DateTime(2026, 6, 15)), "tester", AcctSet));
        Assert.Contains("已结账", ex.Message);
        Assert.Empty(db.Set<FinVoucher>());
    }

    // 调用方直接传入已结账期间的 periodId（如桥接/导入路径）也必须被拒，不只是日期解析路径。
    [Fact]
    public async Task Create_rejects_when_explicit_periodId_refers_to_closed_period()
    {
        await using var db = TestDbContextFactory.Create(nameof(Create_rejects_when_explicit_periodId_refers_to_closed_period), Org);
        await SeedAsync(db, periodNo: 6, isClosed: 1); // 期间 FID=11 已结账
        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var service = VoucherServiceTestHarness.Build(db, http);

        var request = BalancedRequest(new DateTime(2026, 6, 15));
        request.PeriodId = 11; // 显式传入已结账期间，绕过日期解析分支

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, "tester", AcctSet));
        Assert.Contains("已结账", ex.Message);
        Assert.Empty(db.Set<FinVoucher>());
    }

    [Fact]
    public async Task Update_reresolves_period_and_tags_entries_with_org()
    {
        await using var db = TestDbContextFactory.Create(nameof(Update_reresolves_period_and_tags_entries_with_org), Org);
        db.Set<FinAccount>().AddRange(
            VoucherServiceTestHarness.Account(1, "1001", "库存现金", AcctSet, Org),
            VoucherServiceTestHarness.Account(2, "3001", "实收资本", AcctSet, Org));
        db.Set<FinAccountPeriod>().AddRange(
            VoucherServiceTestHarness.Period(10, 2026, 5, AcctSet),
            VoucherServiceTestHarness.Period(11, 2026, 6, AcctSet));
        await db.SaveChangesAsync();
        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var service = VoucherServiceTestHarness.Build(db, http);

        var created = await service.CreateAsync(BalancedRequest(new DateTime(2026, 5, 10)), "tester", AcctSet);
        Assert.Equal(10, db.Set<FinVoucher>().Single(v => v.FID == created.Id).FPeriodId);

        var updateReq = BalancedRequest(new DateTime(2026, 6, 20));
        await service.UpdateAsync(created.Id, updateReq, "modifier");

        var saved = db.Set<FinVoucher>().Single(v => v.FID == created.Id);
        Assert.Equal(11, saved.FPeriodId);
        var entries = db.Set<FinVoucherEntry>().Where(e => e.FVoucherId == created.Id).ToList();
        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Equal(Org, e.FOrgId));
    }

    [Fact]
    public async Task SaveDraft_resolves_period_from_date()
    {
        await using var db = TestDbContextFactory.Create(nameof(SaveDraft_resolves_period_from_date), Org);
        await SeedAsync(db, periodNo: 6);
        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var service = VoucherServiceTestHarness.Build(db, http);

        var dto = await service.SaveDraftAsync(BalancedRequest(new DateTime(2026, 6, 15)), "tester", AcctSet);

        var saved = db.Set<FinVoucher>().Single(v => v.FID == dto.Id);
        Assert.Equal(11, saved.FPeriodId);
        Assert.Equal(0, saved.FStatus); // 草稿
    }

    // 复制/冲销按今日解析期间：今日所在期间已结账时，返回 Fail（而非抛异常致端点 500）。
    [Fact]
    public async Task Copy_returns_fail_when_today_period_closed()
    {
        await using var db = TestDbContextFactory.Create(nameof(Copy_returns_fail_when_today_period_closed), Org);
        var src = await SeedSourceVoucherForTodayAsync(db, todayPeriodClosed: 1);
        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var service = VoucherServiceTestHarness.Build(db, http);

        var res = await service.CopyAsync(src);

        Assert.NotEqual(200, res.Code);
        Assert.Contains("已结账", res.Message);
        Assert.Single(db.Set<FinVoucher>()); // 未生成复制单
    }

    [Fact]
    public async Task Reverse_succeeds_into_open_today_period_and_creates_red_entries()
    {
        await using var db = TestDbContextFactory.Create(nameof(Reverse_succeeds_into_open_today_period_and_creates_red_entries), Org);
        var src = await SeedSourceVoucherForTodayAsync(db, todayPeriodClosed: 0);
        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var service = VoucherServiceTestHarness.Build(db, http);

        var res = await service.ReverseAsync(src);

        Assert.Equal(200, res.Code);
        var redEntries = db.Set<FinVoucherEntry>().Where(e => e.FVoucherId != src).ToList();
        Assert.NotEmpty(redEntries);
        Assert.All(redEntries, e => Assert.Equal(Org, e.FOrgId));       // 红冲分录带组织标记
        Assert.Contains(redEntries, e => e.FDebitAmount < 0 || e.FCreditAmount < 0); // 确为红字
    }

    // 补录提交=过账动作：草稿创建后所在期间已结账时，补录完成必须被拒、状态保持草稿(0)。
    [Fact]
    public async Task CompleteRecord_rejects_when_period_closed()
    {
        await using var db = TestDbContextFactory.Create(nameof(CompleteRecord_rejects_when_period_closed), Org);
        db.Set<FinAccountPeriod>().Add(VoucherServiceTestHarness.Period(11, 2026, 6, AcctSet, isClosed: 1));
        db.Set<FinVoucher>().Add(new FinVoucher
        {
            FID = 600, FVoucherWord = "记", FVoucherNo = 1, FDate = new DateTime(2026, 6, 10),
            FPeriodId = 11, FStatus = 0, FRemark = "[待补录]", FAccountSetId = AcctSet, FOrgId = Org, FCreator = "u"
        });
        db.Set<FinVoucherEntry>().AddRange(
            new FinVoucherEntry { FID = 1, FVoucherId = 600, FLineNo = 1, FAccountId = 1, FDebitAmount = 100m, FOrgId = Org },
            new FinVoucherEntry { FID = 2, FVoucherId = 600, FLineNo = 2, FAccountId = 2, FCreditAmount = 100m, FOrgId = Org });
        await db.SaveChangesAsync();
        var http = VoucherServiceTestHarness.HttpContext(Org, AcctSet);
        var service = VoucherServiceTestHarness.Build(db, http);

        var res = await service.CompleteRecordAsync(600);

        Assert.NotEqual(200, res.Code);
        Assert.Contains("已结账", res.Message);
        Assert.Equal(0, db.Set<FinVoucher>().Single(v => v.FID == 600).FStatus); // 仍是草稿，未提交
    }

    // 播种一张属于今日所在期间的源凭证；todayPeriodClosed 控制今日期间是否已结账。
    private static async Task<long> SeedSourceVoucherForTodayAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, int todayPeriodClosed)
    {
        var today = DateTime.Today;
        db.Set<FinAccountPeriod>().Add(
            VoucherServiceTestHarness.Period(30, today.Year, today.Month, AcctSet, todayPeriodClosed));
        db.Set<FinVoucher>().Add(new FinVoucher
        {
            FID = 700, FVoucherWord = "记", FVoucherNo = 1, FDate = today, FPeriodId = 30,
            FStatus = 2, FAccountSetId = AcctSet, FOrgId = Org, FCreator = "owner"
        });
        db.Set<FinVoucherEntry>().AddRange(
            new FinVoucherEntry { FID = 1, FVoucherId = 700, FLineNo = 1, FAccountId = 1, FDebitAmount = 100m, FOrgId = Org },
            new FinVoucherEntry { FID = 2, FVoucherId = 700, FLineNo = 2, FAccountId = 2, FCreditAmount = 100m, FOrgId = Org });
        await db.SaveChangesAsync();
        return 700;
    }
}
