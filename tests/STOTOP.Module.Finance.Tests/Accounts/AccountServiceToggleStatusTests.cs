using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Accounts;

public class AccountServiceToggleStatusTests
{
    private static AccountService CreateService(STOTOP.Infrastructure.Data.STOTOPDbContext db)
        => new AccountService(
            new Repository<FinAccount>(db),
            new Repository<FinAccountBalance>(db),
            new Repository<FinVoucherEntry>(db),
            null!);

    private static async Task SeedAccountAsync(STOTOP.Infrastructure.Data.STOTOPDbContext db, long id, long parentId = 0, int enableStatus = 1)
    {
        db.Set<FinAccount>().Add(new FinAccount
        {
            FID = id,
            FCode = id.ToString(),
            FName = "科目" + id,
            FCategory = "流动资产",
            FBalanceDirection = "借",
            FLevel = 1,
            FParentId = parentId,
            FIsLeaf = 1,
            FAccountSetId = 1,
            FEnableStatus = enableStatus
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Toggle_disable_rejected_when_referenced_by_voucher()
    {
        await using var db = TestDbContextFactory.Create(nameof(Toggle_disable_rejected_when_referenced_by_voucher));
        await SeedAccountAsync(db, id: 1);
        db.Set<FinVoucherEntry>().Add(new FinVoucherEntry { FAccountId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ToggleStatusAsync(1));
        Assert.Contains("凭证", ex.Message);
        Assert.Equal(1, db.Set<FinAccount>().Single(a => a.FID == 1).FEnableStatus); // 仍启用
    }

    [Fact]
    public async Task Toggle_disable_rejected_when_has_enabled_children()
    {
        await using var db = TestDbContextFactory.Create(nameof(Toggle_disable_rejected_when_has_enabled_children));
        await SeedAccountAsync(db, id: 1);
        await SeedAccountAsync(db, id: 2, parentId: 1, enableStatus: 1);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ToggleStatusAsync(1));
        Assert.Contains("下级", ex.Message);
    }

    [Fact]
    public async Task Toggle_disable_rejected_when_has_balance()
    {
        await using var db = TestDbContextFactory.Create(nameof(Toggle_disable_rejected_when_has_balance));
        await SeedAccountAsync(db, id: 1);
        db.Set<FinAccountBalance>().Add(new FinAccountBalance { FAccountId = 1, FAccountSetId = 1, FPeriodId = 0, FBeginDebit = 100m });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ToggleStatusAsync(1));
        Assert.Contains("余额", ex.Message);
    }

    [Fact]
    public async Task Toggle_disable_succeeds_when_clean()
    {
        await using var db = TestDbContextFactory.Create(nameof(Toggle_disable_succeeds_when_clean));
        await SeedAccountAsync(db, id: 1);
        var service = CreateService(db);

        await service.ToggleStatusAsync(1);
        Assert.Equal(0, db.Set<FinAccount>().Single(a => a.FID == 1).FEnableStatus); // 已停用
    }

    [Fact]
    public async Task Toggle_enable_skips_validation()
    {
        await using var db = TestDbContextFactory.Create(nameof(Toggle_enable_skips_validation));
        await SeedAccountAsync(db, id: 1, enableStatus: 0); // 当前停用
        db.Set<FinVoucherEntry>().Add(new FinVoucherEntry { FAccountId = 1 }); // 即使有凭证
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.ToggleStatusAsync(1); // 启用不校验
        Assert.Equal(1, db.Set<FinAccount>().Single(a => a.FID == 1).FEnableStatus); // 已启用
    }
}
