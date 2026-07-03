using Microsoft.EntityFrameworkCore;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段3B(M6) 经营单元派生自检：FinOperatingUnitDeriver 从 SYS网点公司 1:1 物化派生 FIN经营单元、
/// 双身份对账、公司停用联动、幂等。（放 System.Tests：其模型注册全模块，同时含 SysOutletCompany + FinOperatingUnit。）
/// </summary>
public class FinOperatingUnitTests
{
    private static void AddCompany(Infrastructure.Data.STOTOPDbContext ctx, long id, string name, int status = 1)
        => ctx.Set<SysOutletCompany>().Add(new SysOutletCompany
        { FID = id, FTenantId = 1, FOrgNodeId = id + 1000, FName = name, FStatus = status });

    [Fact]
    public void 派生_1对1_双身份对账_名称同步()
    {
        using var ctx = TestDbContextFactory.Create("ou_derive");
        AddCompany(ctx, 1, "城区子公司");
        AddCompany(ctx, 2, "南郊子公司");
        AddCompany(ctx, 3, "沙溪子公司");
        AddCompany(ctx, 4, "浏河子公司");
        ctx.SaveChanges();

        FinOperatingUnitDeriver.SyncAllFromOutletCompanies(ctx);

        var units = ctx.Set<FinOperatingUnit>().IgnoreQueryFilters().ToList();
        // 双身份对账:经营单元数 == 网点公司数
        Assert.Equal(4, units.Count);
        // 1:1 by FCompanyId + 名称/租户派生
        var byCompany = units.ToDictionary(u => u.FCompanyId);
        Assert.Equal("城区子公司", byCompany[1].FName);
        Assert.Equal(1L, byCompany[1].FTenantId);
        Assert.Equal(1, byCompany[1].FStatus);
        Assert.All(units, u => Assert.Equal(0, u.FCompanyId % 1)); // 每条都有 FCompanyId
    }

    [Fact]
    public void 幂等_重跑不产生重复()
    {
        using var ctx = TestDbContextFactory.Create("ou_idempotent");
        AddCompany(ctx, 1, "城区子公司");
        ctx.SaveChanges();

        FinOperatingUnitDeriver.SyncAllFromOutletCompanies(ctx);
        FinOperatingUnitDeriver.SyncAllFromOutletCompanies(ctx);

        Assert.Single(ctx.Set<FinOperatingUnit>().IgnoreQueryFilters().Where(u => u.FCompanyId == 1));
    }

    [Fact]
    public void 公司停用_联动经营单元停用()
    {
        using var ctx = TestDbContextFactory.Create("ou_disable");
        AddCompany(ctx, 1, "城区子公司");
        ctx.SaveChanges();
        FinOperatingUnitDeriver.SyncAllFromOutletCompanies(ctx);
        Assert.Equal(1, ctx.Set<FinOperatingUnit>().IgnoreQueryFilters().Single(u => u.FCompanyId == 1).FStatus);

        // 停用公司 → 再派生 → 单元停用
        var c = ctx.Set<SysOutletCompany>().IgnoreQueryFilters().Single(x => x.FID == 1);
        c.FStatus = 0;
        ctx.SaveChanges();
        FinOperatingUnitDeriver.SyncAllFromOutletCompanies(ctx);
        Assert.Equal(0, ctx.Set<FinOperatingUnit>().IgnoreQueryFilters().Single(u => u.FCompanyId == 1).FStatus);
    }
}
