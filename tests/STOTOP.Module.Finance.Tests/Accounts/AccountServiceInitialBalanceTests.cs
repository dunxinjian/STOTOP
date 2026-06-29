using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Accounts;

public class AccountServiceInitialBalanceTests
{
    private static AccountService CreateService(STOTOP.Infrastructure.Data.STOTOPDbContext db)
        => new AccountService(
            new Repository<FinAccount>(db),
            new Repository<FinAccountBalance>(db),
            null!, null!);

    private static async Task SeedTwoLeafAccountsAsync(STOTOP.Infrastructure.Data.STOTOPDbContext db, long accountSetId)
    {
        db.Set<FinAccount>().AddRange(
            new FinAccount { FID = 1, FCode = "1001", FName = "库存现金", FCategory = "流动资产", FBalanceDirection = "借", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = accountSetId, FEnableStatus = 1 },
            new FinAccount { FID = 2, FCode = "3001", FName = "实收资本", FCategory = "所有者权益", FBalanceDirection = "贷", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = accountSetId, FEnableStatus = 1 }
        );
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SaveInitialBalances_throws_and_persists_nothing_when_unbalanced()
    {
        await using var db = TestDbContextFactory.Create(nameof(SaveInitialBalances_throws_and_persists_nothing_when_unbalanced));
        await SeedTwoLeafAccountsAsync(db, accountSetId: 1);
        var service = CreateService(db);

        var request = new SaveInitialBalancesRequest
        {
            AccountSetId = 1,
            Items = new List<InitialBalanceItem>
            {
                new() { AccountId = 1, DebitBalance = 1000m, CreditBalance = 0m },
                new() { AccountId = 2, DebitBalance = 0m, CreditBalance = 800m }, // 借1000 vs 贷800 不平
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveInitialBalancesAsync(request));
        Assert.Contains("不平衡", ex.Message);
        Assert.Empty(db.Set<FinAccountBalance>()); // 不平时不落库
    }

    [Fact]
    public async Task SaveInitialBalances_persists_when_balanced()
    {
        await using var db = TestDbContextFactory.Create(nameof(SaveInitialBalances_persists_when_balanced));
        await SeedTwoLeafAccountsAsync(db, accountSetId: 1);
        var service = CreateService(db);

        var request = new SaveInitialBalancesRequest
        {
            AccountSetId = 1,
            Items = new List<InitialBalanceItem>
            {
                new() { AccountId = 1, DebitBalance = 1000m, CreditBalance = 0m },
                new() { AccountId = 2, DebitBalance = 0m, CreditBalance = 1000m },
            }
        };

        await service.SaveInitialBalancesAsync(request);

        var balances = db.Set<FinAccountBalance>().ToList();
        Assert.Equal(2, balances.Count);
        Assert.Equal(1000m, balances.Single(b => b.FAccountId == 1).FBeginDebit);
        Assert.Equal(1000m, balances.Single(b => b.FAccountId == 2).FBeginCredit);
    }

    [Fact]
    public async Task SaveInitialBalances_throws_when_profit_loss_account_has_nonzero()
    {
        await using var db = TestDbContextFactory.Create(nameof(SaveInitialBalances_throws_when_profit_loss_account_has_nonzero));
        db.Set<FinAccount>().AddRange(
            new FinAccount { FID = 1, FCode = "1001", FName = "库存现金", FCategory = "流动资产", FBalanceDirection = "借", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = 1, FEnableStatus = 1 },
            new FinAccount { FID = 5, FCode = "5001", FName = "主营业务收入", FCategory = "营业收入", FBalanceDirection = "贷", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = 1, FEnableStatus = 1 }
        );
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var request = new SaveInitialBalancesRequest
        {
            AccountSetId = 1,
            Items = new List<InitialBalanceItem>
            {
                new() { AccountId = 1, DebitBalance = 1000m, CreditBalance = 0m },
                new() { AccountId = 5, DebitBalance = 0m, CreditBalance = 1000m },
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveInitialBalancesAsync(request));
        Assert.Contains("损益", ex.Message);
        Assert.Empty(db.Set<FinAccountBalance>());
    }

    [Fact]
    public async Task SaveInitialBalances_allows_profit_loss_account_with_zero()
    {
        await using var db = TestDbContextFactory.Create(nameof(SaveInitialBalances_allows_profit_loss_account_with_zero));
        db.Set<FinAccount>().AddRange(
            new FinAccount { FID = 1, FCode = "1001", FName = "库存现金", FCategory = "流动资产", FBalanceDirection = "借", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = 1, FEnableStatus = 1 },
            new FinAccount { FID = 3, FCode = "3001", FName = "实收资本", FCategory = "所有者权益", FBalanceDirection = "贷", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = 1, FEnableStatus = 1 },
            new FinAccount { FID = 5, FCode = "5001", FName = "主营业务收入", FCategory = "营业收入", FBalanceDirection = "贷", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = 1, FEnableStatus = 1 }
        );
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var request = new SaveInitialBalancesRequest
        {
            AccountSetId = 1,
            Items = new List<InitialBalanceItem>
            {
                new() { AccountId = 1, DebitBalance = 1000m, CreditBalance = 0m },
                new() { AccountId = 3, DebitBalance = 0m, CreditBalance = 1000m },
                new() { AccountId = 5, DebitBalance = 0m, CreditBalance = 0m },
            }
        };

        await service.SaveInitialBalancesAsync(request);
        Assert.Equal(1000m, db.Set<FinAccountBalance>().Single(b => b.FAccountId == 1).FBeginDebit);
        Assert.Equal(1000m, db.Set<FinAccountBalance>().Single(b => b.FAccountId == 3).FBeginCredit);
    }
}
