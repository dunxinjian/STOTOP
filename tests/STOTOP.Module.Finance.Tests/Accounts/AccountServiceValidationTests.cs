using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Accounts;

public class AccountServiceValidationTests
{
    private static AccountService CreateService(STOTOP.Infrastructure.Data.STOTOPDbContext db)
        => new AccountService(
            new Repository<FinAccount>(db),
            new Repository<FinAccountBalance>(db),
            new Repository<FinVoucherEntry>(db),
            null!);

    [Fact]
    public async Task Create_rejects_empty_name()
    {
        await using var db = TestDbContextFactory.Create(nameof(Create_rejects_empty_name));
        var service = CreateService(db);
        var req = new CreateAccountRequest { Code = "1001", Name = "  ", Category = "流动资产", BalanceDirection = "借", ParentId = 0 };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, 1));
        Assert.Contains("名称", ex.Message);
    }

    [Fact]
    public async Task Create_rejects_invalid_direction()
    {
        await using var db = TestDbContextFactory.Create(nameof(Create_rejects_invalid_direction));
        var service = CreateService(db);
        var req = new CreateAccountRequest { Code = "1001", Name = "库存现金", Category = "流动资产", BalanceDirection = "x", ParentId = 0 };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, 1));
        Assert.Contains("方向", ex.Message);
    }

    [Fact]
    public async Task Create_succeeds_with_valid_input()
    {
        await using var db = TestDbContextFactory.Create(nameof(Create_succeeds_with_valid_input));
        var service = CreateService(db);
        var req = new CreateAccountRequest { Code = "1001", Name = "库存现金", Category = "流动资产", BalanceDirection = "借", ParentId = 0 };

        var dto = await service.CreateAsync(req, 1);

        Assert.Equal("1001", dto.Code);
        Assert.Equal("借", dto.BalanceDirection);
    }

    [Fact]
    public async Task Update_rejects_empty_name()
    {
        await using var db = TestDbContextFactory.Create(nameof(Update_rejects_empty_name));
        db.Set<FinAccount>().Add(new FinAccount { FID = 1, FCode = "1001", FName = "库存现金", FCategory = "流动资产", FBalanceDirection = "借", FLevel = 1, FParentId = 0, FIsLeaf = 1, FAccountSetId = 1, FEnableStatus = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(1, new UpdateAccountRequest { Name = "" }));
        Assert.Contains("名称", ex.Message);
    }
}
