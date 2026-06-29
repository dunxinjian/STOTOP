using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Accounts;

public class ApplyTemplateTests
{
    private static STOTOP.Infrastructure.Data.STOTOPDbContext CreateDbIgnoringTx(string name)
    {
        STOTOP.Infrastructure.Data.STOTOPDbContext.RegisterModuleAssembly(typeof(FinAccountTemplate).Assembly);
        var options = new DbContextOptionsBuilder<STOTOP.Infrastructure.Data.STOTOPDbContext>()
            .UseInMemoryDatabase($"{name}_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new STOTOP.Infrastructure.Data.STOTOPDbContext(options, null);
    }

    private static AccountTemplateService CreateService(STOTOP.Infrastructure.Data.STOTOPDbContext db)
        => new AccountTemplateService(
            new Repository<FinAccountTemplate>(db),
            new Repository<FinAccountTemplateItem>(db),
            new Repository<FinAccount>(db),
            new HttpContextAccessor(),
            db);

    [Fact]
    public async Task ApplyTemplate_builds_accounts_and_preserves_parent_child()
    {
        await using var db = CreateDbIgnoringTx(nameof(ApplyTemplate_builds_accounts_and_preserves_parent_child));
        db.Set<FinAccountTemplate>().Add(new FinAccountTemplate { FID = 3, FCode = "express-delivery", FName = "快递行业标准模板", FIsPreset = 1, FEnableStatus = 1 });
        db.Set<FinAccountTemplateItem>().AddRange(
            new FinAccountTemplateItem { FID = 10, FTemplateId = 3, FCode = "1002", FName = "银行存款", FCategory = "流动资产", FBalanceDirection = "借", FLevel = 1, FParentId = 0, FIsLeaf = 1 },
            new FinAccountTemplateItem { FID = 11, FTemplateId = 3, FCode = "5001", FName = "主营业务收入", FCategory = "营业收入", FBalanceDirection = "贷", FLevel = 1, FParentId = 0, FIsLeaf = 0 },
            new FinAccountTemplateItem { FID = 12, FTemplateId = 3, FCode = "500101", FName = "出港收入", FCategory = "营业收入", FBalanceDirection = "贷", FLevel = 2, FParentId = 11, FIsLeaf = 1 }
        );
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ApplyTemplateAsync(3, accountSetId: 99);

        var accounts = db.Set<FinAccount>().Where(a => a.FAccountSetId == 99).ToList();
        Assert.Equal(3, accounts.Count);
        Assert.Equal(1, accounts.Single(a => a.FCode == "1002").FIsLeaf);
        var revenue = accounts.Single(a => a.FCode == "5001");
        var child = accounts.Single(a => a.FCode == "500101");
        Assert.Equal(revenue.FID, child.FParentId);
        Assert.NotEqual(11, child.FParentId);
    }

    [Fact]
    public async Task ApplyTemplate_throws_when_template_has_no_items()
    {
        await using var db = CreateDbIgnoringTx(nameof(ApplyTemplate_throws_when_template_has_no_items));
        db.Set<FinAccountTemplate>().Add(new FinAccountTemplate { FID = 3, FCode = "express-delivery", FName = "快递行业标准模板" });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyTemplateAsync(3, accountSetId: 99));
    }
}
