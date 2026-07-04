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
    public void 交叉引用桥_公司名去子_匹配businessUnit_aux_回填来源()
    {
        using var ctx = TestDbContextFactory.Create("ou_crosswalk");
        AddCompany(ctx, 1, "城区子公司");
        AddCompany(ctx, 2, "南郊子公司");   // 故意不建对应 aux → 应为 null
        // business_unit aux：规范名 = 公司名去"子"(城区子公司→城区公司)
        ctx.Set<FinAuxiliaryItem>().Add(new FinAuxiliaryItem { FID = 101, FTenantId = 1, FOrgId = 192, FAuxType = "business_unit", FCode = "BU-CQ", FName = "城区公司" });
        ctx.Set<FinAuxiliaryItem>().Add(new FinAuxiliaryItem { FID = 106, FTenantId = 1, FOrgId = 192, FAuxType = "business_unit", FCode = "BU-CG", FName = "出港业务" }); // 方向:无公司→不被桥
        ctx.SaveChanges();

        FinOperatingUnitDeriver.SyncAllFromOutletCompanies(ctx);

        var byCompany = ctx.Set<FinOperatingUnit>().IgnoreQueryFilters().ToDictionary(u => u.FCompanyId);
        Assert.Equal("SYS网点公司", byCompany[1].FSourceType);
        Assert.Equal(101L, byCompany[1].FSourceLegacyAuxId);   // 城区子公司→城区公司→BU-CQ(101)
        Assert.Null(byCompany[2].FSourceLegacyAuxId);          // 南郊无对应 aux → null
        // 桥只覆盖网点公司级,出港业务(方向) aux 不被任何经营单元引用
        Assert.DoesNotContain(byCompany.Values, u => u.FSourceLegacyAuxId == 106L);

        // 桥单向存于 OU 侧：**不**反标 business_unit aux 的来源(否则冻结其改名)——aux 侧应保持 null
        var auxById = ctx.Set<FinAuxiliaryItem>().IgnoreQueryFilters().ToDictionary(a => a.FID);
        Assert.Null(auxById[101].FSourceType);
        Assert.Null(auxById[101].FSourceId);
    }

    [Fact]
    public void 交叉引用桥_同租户同规范名多aux_确定性取最小FID()
    {
        using var ctx = TestDbContextFactory.Create("ou_crosswalk_dup");
        AddCompany(ctx, 1, "城区子公司");
        // 同租户同名两条 business_unit aux(账套维度可重名,无唯一约束)——桥须确定性取最小 FID
        ctx.Set<FinAuxiliaryItem>().Add(new FinAuxiliaryItem { FID = 300, FTenantId = 1, FOrgId = 192, FAccountSetId = 2, FAuxType = "business_unit", FCode = "BU-CQ2", FName = "城区公司" });
        ctx.Set<FinAuxiliaryItem>().Add(new FinAuxiliaryItem { FID = 200, FTenantId = 1, FOrgId = 192, FAccountSetId = 1, FAuxType = "business_unit", FCode = "BU-CQ1", FName = "城区公司" });
        ctx.SaveChanges();

        FinOperatingUnitDeriver.SyncAllFromOutletCompanies(ctx);

        var ou = ctx.Set<FinOperatingUnit>().IgnoreQueryFilters().Single(u => u.FCompanyId == 1);
        Assert.Equal(200L, ou.FSourceLegacyAuxId);  // 取最小 FID(200)而非随机
    }

    [Fact]
    public void 交叉引用桥_aux晚于首次派生_重跑补建()   // 复现 fresh-DB tier 顺序:business_unit aux(BasicData tier)晚于 OU 首次派生(Finance V17/V18)
    {
        using var ctx = TestDbContextFactory.Create("ou_late_aux");
        AddCompany(ctx, 1, "城区子公司");
        ctx.SaveChanges();

        // ① 首次派生(模拟 Finance tier:此刻 business_unit aux 尚未播种)→ OU 建成但桥空(无害)
        FinOperatingUnitDeriver.SyncAllFromOutletCompanies(ctx);
        Assert.Null(ctx.Set<FinOperatingUnit>().IgnoreQueryFilters().Single(u => u.FCompanyId == 1).FSourceLegacyAuxId);

        // ② aux 播种(模拟 BasicDataSeeder.SeedBUAuxiliary)
        ctx.Set<FinAuxiliaryItem>().Add(new FinAuxiliaryItem { FID = 101, FTenantId = 1, FOrgId = 192, FAuxType = "business_unit", FCode = "BU-CQ", FName = "城区公司" });
        ctx.SaveChanges();

        // ③ 重跑派生(模拟 BasicData V1 内 SeedBUAuxiliary 之后的补建调用)→ 桥补齐(fresh-DB 建桥点)
        FinOperatingUnitDeriver.SyncAllFromOutletCompanies(ctx);
        var ou = ctx.Set<FinOperatingUnit>().IgnoreQueryFilters().Single(u => u.FCompanyId == 1);
        Assert.Equal(101L, ou.FSourceLegacyAuxId);
        // 桥单向:aux 侧不被反标(保可改名)
        Assert.Null(ctx.Set<FinAuxiliaryItem>().IgnoreQueryFilters().Single(a => a.FID == 101).FSourceType);
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
