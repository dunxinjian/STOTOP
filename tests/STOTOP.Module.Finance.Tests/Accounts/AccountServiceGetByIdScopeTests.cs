using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Accounts;

public class AccountServiceGetByIdScopeTests
{
    private static AccountService CreateService(STOTOP.Infrastructure.Data.STOTOPDbContext db)
        => new AccountService(
            new Repository<FinAccount>(db),
            new Repository<FinAccountBalance>(db),
            new Repository<FinVoucherEntry>(db),
            null!);

    [Fact]
    public async Task GetById_returns_account_when_accountset_matches()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetById_returns_account_when_accountset_matches));
        db.Set<FinAccount>().Add(new FinAccount { FID = 1, FCode = "1001", FName = "库存现金", FCategory = "流动资产", FBalanceDirection = "借", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = 1, FEnableStatus = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var dto = await service.GetByIdAsync(1, 1);

        Assert.NotNull(dto);
        Assert.Equal("1001", dto!.Code);
    }

    [Fact]
    public async Task GetById_returns_null_when_accountset_mismatch()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetById_returns_null_when_accountset_mismatch));
        db.Set<FinAccount>().Add(new FinAccount { FID = 1, FCode = "1001", FName = "库存现金", FCategory = "流动资产", FBalanceDirection = "借", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = 1, FEnableStatus = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var dto = await service.GetByIdAsync(1, 2); // 用账套2查账套1的科目 → 应视为不存在

        Assert.Null(dto);
    }
}
