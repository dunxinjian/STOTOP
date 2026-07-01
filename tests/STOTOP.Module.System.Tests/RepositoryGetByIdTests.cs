using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// Repository.GetByIdAsync 回归自检（阶段1 IDOR 硬化）：
/// ① 主键属性名从模型元数据解析（不硬编码 "FID"），对主键名为 Id 的实体也正确命中；
/// ② 经全局查询过滤器——租户实体他租户读不到（关闭裸 FindAsync 绕过滤器的越权直查）。
/// </summary>
public class RepositoryGetByIdTests
{
    // 主键名为 Id（非 FID）的实体：回归 EF.Property<long>(e,"FID") 硬编码致其查询翻译期抛的 critical 缺陷。
    [Fact]
    public async global::System.Threading.Tasks.Task GetByIdAsync_主键名为Id的实体_按真实主键命中()
    {
        using var ctx = TestDbContextFactory.Create("repo_idkey");
        var scope = new ExpPriceSurchargeScope { FSurchargeId = 1, FLinkedType = "KH", FLinkedId = "C001" };
        ctx.Set<ExpPriceSurchargeScope>().Add(scope);
        await ctx.SaveChangesAsync();

        var repo = new Repository<ExpPriceSurchargeScope>(ctx);
        var found = await repo.GetByIdAsync(scope.Id);

        Assert.NotNull(found);
        Assert.Equal(scope.Id, found!.Id);
    }

    // FID 主键的租户实体：GetByIdAsync 经全局过滤器——本租户命中、他租户读不到（IDOR 关闭）。
    [Fact]
    public async global::System.Threading.Tasks.Task GetByIdAsync_FID租户实体_经过滤器_他租户读不到()
    {
        TenantTestModules.RegisterAll();
        var options = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseInMemoryDatabase($"repo_idor_{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;

        long id;
        using (var a = new STOTOPDbContext(options, new TestDbContextFactory.TestContextAccessor { CurrentTenantId = 10 }))
        {
            var card = new SysFeedbackCard { FTitle = "反馈", FModule = "test", FSubmitterId = 1 };
            a.Set<SysFeedbackCard>().Add(card);
            await a.SaveChangesAsync();
            id = card.FID;
        }

        // 本租户 10：命中
        using (var a2 = new STOTOPDbContext(options, new TestDbContextFactory.TestContextAccessor { CurrentTenantId = 10 }))
        {
            var repo = new Repository<SysFeedbackCard>(a2);
            Assert.NotNull(await repo.GetByIdAsync(id));
        }

        // 他租户 20：读不到（过滤器生效，杜绝裸 FindAsync 越权直查）
        using (var b = new STOTOPDbContext(options, new TestDbContextFactory.TestContextAccessor { CurrentTenantId = 20 }))
        {
            var repo = new Repository<SysFeedbackCard>(b);
            Assert.Null(await repo.GetByIdAsync(id));
        }
    }
}
