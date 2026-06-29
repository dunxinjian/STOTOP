using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Accounts;

public class AccountServiceGetByAuxTypeTests
{
    private static AccountService CreateService(STOTOP.Infrastructure.Data.STOTOPDbContext db)
        => new AccountService(
            new Repository<FinAccount>(db),
            new Repository<FinAccountBalance>(db),
            new Repository<FinVoucherEntry>(db),
            null!);

    [Fact]
    public async Task GetByAuxType_exact_match_not_prefix()
    {
        // dept 是 dept_special 的前缀子串，Contains("dept") 会误命中 dept_special
        await using var db = TestDbContextFactory.Create(nameof(GetByAuxType_exact_match_not_prefix));
        db.Set<FinAccount>().AddRange(
            new FinAccount { FID = 1, FCode = "1122", FName = "应收账款", FCategory = "流动资产", FBalanceDirection = "借", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = 1, FEnableStatus = 1, FAuxiliary = "dept,customer" },
            new FinAccount { FID = 2, FCode = "1601", FName = "固定资产", FCategory = "非流动资产", FBalanceDirection = "借", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = 1, FEnableStatus = 1, FAuxiliary = "dept_special" }
        );
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetByAuxTypeAsync("dept", 1);

        Assert.Single(result);            // 改前 Contains("dept") 会误命中 "dept_special" → 2 条
        Assert.Equal("1122", result[0].Code);
    }
}
