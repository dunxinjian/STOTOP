using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Quality.Services.Unification;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Unify;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL Task 命名空间，恢复别名
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// Plan2 Phase B：主数据匹配 MasterDataMatcher 单测（纯 InMemory，不碰真库）。
/// 网点解析 4 级：编码 / 简称 / 全称 / 别名 / 未匹配。
/// 员工解析 4 级：工号(1) / 别名(2) / 启发式唯一命中(3) / 启发式多命中不绑 / 未匹配(0)。
/// </summary>
public class MasterDataMatcherTests
{
    private const long Org = 100L;
    private const long OtherOrg = 999L;

    private static STOTOPDbContext NewDb()
    {
        var db = TestDbContextFactory.Create("MatcherTests");
        // 网点主数据
        db.Set<ExpNetworkPoint>().AddRange(
            new ExpNetworkPoint { FCode = "NP001", FShortName = "城西站", FFullName = "杭州城西分拨中心", FOrgId = Org, FOwnerOrgId = Org },
            new ExpNetworkPoint { FCode = "NP002", FShortName = "城东站", FFullName = "杭州城东分拨中心", FOrgId = Org, FOwnerOrgId = Org },
            // 跨组织同名网点，验证 orgId 隔离
            new ExpNetworkPoint { FCode = "NPX01", FShortName = "城西站", FFullName = "他组织城西", FOrgId = OtherOrg, FOwnerOrgId = OtherOrg }
        );
        // 网点别名
        db.Set<ExpNetworkPointAlias>().AddRange(
            new ExpNetworkPointAlias { FName = "城西转运", FNetworkPointCode = "NP001", FOrgId = Org }
        );
        // 员工主数据（NP001 网点内：张三、张四；NP002 网点内：李四）
        db.Set<ExpSalesman>().AddRange(
            new ExpSalesman { FEmployeeNo = "E001", FName = "张三", FNetworkPointCode = "NP001", FEmployeeId = 11 },
            new ExpSalesman { FEmployeeNo = "E002", FName = "李四", FNetworkPointCode = "NP002", FEmployeeId = 12 },
            // 同网点同名两人，验证启发式多命中不绑
            new ExpSalesman { FEmployeeNo = "E003", FName = "王五", FNetworkPointCode = "NP001", FEmployeeId = 13 },
            new ExpSalesman { FEmployeeNo = "E004", FName = "王五", FNetworkPointCode = "NP001", FEmployeeId = 14 }
        );
        // 员工别名
        db.Set<ExpSalesmanAlias>().AddRange(
            new ExpSalesmanAlias { FName = "小三", FEmployeeNo = "E001", FOrgId = Org }
        );
        db.SaveChanges();
        return db;
    }

    private static IMasterDataMatcher Matcher(STOTOPDbContext db) => new MasterDataMatcher(db);

    // ===== 网点解析 =====

    [Fact]
    public async Task ResolveNetwork_ByCode_Matched()
    {
        await using var db = NewDb();
        var r = await Matcher(db).ResolveNetworkAsync("NP001", "随便", Org);
        Assert.Equal("NP001", r.Code);
        Assert.Equal(1, r.Status);
    }

    [Fact]
    public async Task ResolveNetwork_ByShortName_Matched()
    {
        await using var db = NewDb();
        var r = await Matcher(db).ResolveNetworkAsync(null, "城西站", Org);
        Assert.Equal("NP001", r.Code);
        Assert.Equal(1, r.Status);
    }

    [Fact]
    public async Task ResolveNetwork_ByFullName_Matched()
    {
        await using var db = NewDb();
        var r = await Matcher(db).ResolveNetworkAsync(null, "杭州城东分拨中心", Org);
        Assert.Equal("NP002", r.Code);
        Assert.Equal(1, r.Status);
    }

    [Fact]
    public async Task ResolveNetwork_ByAlias_Matched()
    {
        await using var db = NewDb();
        var r = await Matcher(db).ResolveNetworkAsync(null, "城西转运", Org);
        Assert.Equal("NP001", r.Code);
        Assert.Equal(1, r.Status);
    }

    [Fact]
    public async Task ResolveNetwork_NoMatch_ReturnsStatus0()
    {
        await using var db = NewDb();
        var r = await Matcher(db).ResolveNetworkAsync(null, "不存在的网点", Org);
        Assert.Null(r.Code);
        Assert.Equal("不存在的网点", r.NameRaw);
        Assert.Equal(0, r.Status);
    }

    [Fact]
    public async Task ResolveNetwork_OrgIsolation_OtherOrgCodeNotMatched()
    {
        await using var db = NewDb();
        // NPX01 属于 OtherOrg，在 Org 下按编码查不到
        var r = await Matcher(db).ResolveNetworkAsync("NPX01", null, Org);
        Assert.Null(r.Code);
        Assert.Equal(0, r.Status);
    }

    // ===== 员工解析 =====

    [Fact]
    public async Task ResolveEmployee_ByEmployeeNo_Status1()
    {
        await using var db = NewDb();
        var r = await Matcher(db).ResolveEmployeeAsync("E001", "随便", "NP001", Org);
        Assert.Equal("E001", r.EmployeeNo);
        Assert.Equal(11, r.EmployeeId);
        Assert.Equal(1, r.Status);
    }

    [Fact]
    public async Task ResolveEmployee_ByAlias_Status2()
    {
        await using var db = NewDb();
        var r = await Matcher(db).ResolveEmployeeAsync(null, "小三", "NP001", Org);
        Assert.Equal("E001", r.EmployeeNo);
        Assert.Equal(11, r.EmployeeId);
        Assert.Equal("小三", r.NameRaw);
        Assert.Equal(2, r.Status);
    }

    [Fact]
    public async Task ResolveEmployee_Heuristic_UniqueHit_Status3()
    {
        await using var db = NewDb();
        // "城区张三13900001111" → 清洗去前缀+手机号 → "张三"，NP001 网点内唯一命中 E001
        var r = await Matcher(db).ResolveEmployeeAsync(null, "城区张三13900001111", "NP001", Org);
        Assert.Equal("E001", r.EmployeeNo);
        Assert.Equal(11, r.EmployeeId);
        Assert.Equal(3, r.Status);
    }

    [Fact]
    public async Task ResolveEmployee_Heuristic_MultiHit_NotBound()
    {
        await using var db = NewDb();
        // "王五" 在 NP001 网点内有两人（E003/E004），多命中不绑 → Status 0
        var r = await Matcher(db).ResolveEmployeeAsync(null, "操作部王五", "NP001", Org);
        Assert.Null(r.EmployeeNo);
        Assert.Null(r.EmployeeId);
        Assert.Equal(0, r.Status);
    }

    [Fact]
    public async Task ResolveEmployee_NoMatch_Status0()
    {
        await using var db = NewDb();
        var r = await Matcher(db).ResolveEmployeeAsync(null, "查无此人", "NP001", Org);
        Assert.Null(r.EmployeeNo);
        Assert.Null(r.EmployeeId);
        Assert.Equal("查无此人", r.NameRaw);
        Assert.Equal(0, r.Status);
    }

    [Fact]
    public async Task ResolveEmployee_Heuristic_WrongNetwork_NotBound()
    {
        await using var db = NewDb();
        // "张三" 真身在 NP001；若 networkCode 给 NP002，则网点内查不到 → Status 0
        var r = await Matcher(db).ResolveEmployeeAsync(null, "张三", "NP002", Org);
        Assert.Null(r.EmployeeNo);
        Assert.Equal(0, r.Status);
    }
}
