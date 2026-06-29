using Microsoft.EntityFrameworkCore;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Voucher;

public class VoucherServiceIsolationTests
{
    // 库里有一张属于 组织100/账套7 的凭证（FOrgId 显式=100，不会被 SaveChanges 自动回写，因为非 0）
    private static async Task<long> SeedForeignVoucherAsync(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        var v = new FinVoucher
        {
            FID = 500, FVoucherWord = "记", FVoucherNo = 1, FDate = new DateTime(2026, 6, 1),
            FPeriodId = 11, FStatus = 1, FAccountSetId = 7, FOrgId = 100, FCreator = "owner"
        };
        db.Set<FinVoucher>().Add(v);
        db.Set<FinVoucherEntry>().AddRange(
            new FinVoucherEntry { FID = 1, FVoucherId = 500, FLineNo = 1, FAccountId = 1, FDebitAmount = 100m, FOrgId = 100 },
            new FinVoucherEntry { FID = 2, FVoucherId = 500, FLineNo = 2, FAccountId = 2, FCreditAmount = 100m, FOrgId = 100 });
        await db.SaveChangesAsync();
        return v.FID;
    }

    // 账套维度无全局过滤器：GetById 走 Query() 组织过滤已挡住跨组织，真实缺口是跨账套。
    [Fact]
    public async Task GetById_returns_null_for_other_account_set()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetById_returns_null_for_other_account_set), orgId: 100);
        var id = await SeedForeignVoucherAsync(db);
        var http = VoucherServiceTestHarness.HttpContext(orgId: 100, accountSetId: 8); // 同组织、账套8≠7
        var service = VoucherServiceTestHarness.Build(db, http);

        var dto = await service.GetByIdAsync(id);

        Assert.Null(dto);
    }

    // Delete 走 GetByIdAsync(FindAsync 绕过全局过滤器)：跨账套缺口。
    [Fact]
    public async Task Delete_rejects_other_account_set_same_org()
    {
        await using var db = TestDbContextFactory.Create(nameof(Delete_rejects_other_account_set_same_org), orgId: 100);
        var id = await SeedForeignVoucherAsync(db);
        var http = VoucherServiceTestHarness.HttpContext(orgId: 100, accountSetId: 8);
        var service = VoucherServiceTestHarness.Build(db, http);

        var ok = await service.DeleteAsync(id);

        Assert.False(ok);
        Assert.Single(db.Set<FinVoucher>()); // 未删除
    }

    // Audit 改走 GetOwnedVoucherAsync：跨组织凭证因 Query() 全局组织过滤器不可见 → 返回 null → 拒绝。
    [Fact]
    public async Task Audit_rejects_other_org()
    {
        await using var db = TestDbContextFactory.Create(nameof(Audit_rejects_other_org), orgId: 999);
        var id = await SeedForeignVoucherAsync(db);
        var http = VoucherServiceTestHarness.HttpContext(orgId: 999, accountSetId: 7);
        var service = VoucherServiceTestHarness.Build(db, http);

        var ok = await service.AuditAsync(id, "attacker");

        Assert.False(ok);
        // 跨组织读取需 IgnoreQueryFilters（当前 org=999 过滤器会隐藏 org=100 的凭证）
        Assert.Equal(1, db.Set<FinVoucher>().IgnoreQueryFilters().Single().FStatus);
    }

    // Update 是唯一"取实体后改字段并 SaveChanges"的写路径，跨账套必须被拒、原数据不被改写。
    [Fact]
    public async Task Update_rejects_other_account_set_same_org()
    {
        await using var db = TestDbContextFactory.Create(nameof(Update_rejects_other_account_set_same_org), orgId: 100);
        var id = await SeedForeignVoucherAsync(db);
        var http = VoucherServiceTestHarness.HttpContext(orgId: 100, accountSetId: 8); // 同组织、账套8≠7
        var service = VoucherServiceTestHarness.Build(db, http);

        var req = new CreateVoucherRequest
        {
            VoucherWord = "记",
            Date = new DateTime(2026, 6, 1),
            PeriodId = 11,
            Entries =
            {
                new CreateVoucherEntryRequest { LineNo = 1, Summary = "篡改", AccountId = 1, DebitAmount = 50m },
                new CreateVoucherEntryRequest { LineNo = 2, Summary = "篡改", AccountId = 2, CreditAmount = 50m },
            }
        };

        var result = await service.UpdateAsync(id, req, "attacker");

        Assert.Null(result); // 越账套：视为不存在，不更新
        // 原分录金额仍为 100（未被改写成 50）
        var entries = db.Set<FinVoucherEntry>().Where(e => e.FVoucherId == id).ToList();
        Assert.All(entries, e => Assert.True(e.FDebitAmount == 100m || e.FCreditAmount == 100m));
    }
}
