using STOTOP.Module.Finance.Entities;
using STOTOP.Module.CardFlow.Services.Handlers;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Handlers;

/// <summary>
/// [缺陷5] AutoVoucherHandler.FilterAuxByOrgAndTenant 组织+租户过滤回归。
/// 该方法为纯 LINQ 谓词链，用 List.AsQueryable() 测（与生产 EF IQueryable 同语义），
/// 不触 DbContext / 写回填。验证：全局项(FOrgId==0)豁免租户、组织级项按批次租户隔离、单租户行为不变、
/// 无租户上下文退化为纯组织过滤、停用项剔除。
/// </summary>
public class AutoVoucherHandlerAuxFilterTests
{
    private static FinAuxiliaryItem Aux(long id, string code, long orgId, long tenantId, int enable = 1)
        => new() { FID = id, FAuxType = "x", FCode = code, FName = code, FEnableStatus = enable, FOrgId = orgId, FTenantId = tenantId };

    private static List<long> Filter(long orgId, long? tenantId, params FinAuxiliaryItem[] items)
        => AutoVoucherHandler.FilterAuxByOrgAndTenant(items.AsQueryable(), orgId, tenantId)
            .Select(a => a.FID).OrderBy(x => x).ToList();

    [Fact]
    public void 单租户_全局项本组织项及哨兵全加载_跨组织不加载()
    {
        // 现网单客户：outlet(AddFromNetworkPoint 运行时建)FTenantId=根(1)；business_unit(BasicData 种)FTenantId=0 哨兵；
        // 全局 FOrgId=0。三类都须加载，别组织剔除——即行为与改前一致(关键回归保护)。
        var ids = Filter(192, 1,
            Aux(1, "ST", 0, 1),        // 全局 express_brand
            Aux(2, "OUT", 0, 1),       // 全局 business_direction
            Aux(3, "320288", 192, 1),  // 本组织 outlet(FTenantId=根)
            Aux(4, "BU-CQ", 192, 0),   // 本组织 business_unit(现网 FTenantId=0 哨兵) → 必须加载
            Aux(5, "999", 2, 1));      // 别组织(石家庄 org2) → 不加载
        Assert.Equal(new List<long> { 1, 2, 3, 4 }, ids);
    }

    [Fact]
    public void 多租户_全局项与哨兵豁免_已分配跨租户项剔除()
    {
        // 多租户上线后：太仓租户=192；全局项 FOrgId=0(平台共享)；本租户组织级 FTenantId=192；business_unit 哨兵 FTenantId=0
        var ids = Filter(192, 192,
            Aux(1, "ST", 0, 1),          // 全局 FOrgId=0 → 豁免租户 → 加载
            Aux(3, "320288", 192, 192),  // 本租户组织级 → 加载
            Aux(7, "BU-CQ", 192, 0),     // 未分配哨兵 FTenantId=0(BasicData 种的 business_unit) → 加载(靠 FOrgId 隔离)
            Aux(6, "888", 192, 2));      // FOrgId=192 且 FTenantId=2(已明确分配到别租户) → 剔除(纵深防御)
        Assert.Equal(new List<long> { 1, 3, 7 }, ids);
    }

    [Fact]
    public void 无租户上下文_退化为纯组织过滤_不破坏()
    {
        var ids = Filter(192, null,
            Aux(1, "ST", 0, 5),        // FOrgId=0 → 加载(不看租户)
            Aux(3, "320288", 192, 9),  // FOrgId=192 → 加载(不看租户)
            Aux(5, "999", 2, 1));      // 别组织 → 不加载
        Assert.Equal(new List<long> { 1, 3 }, ids);
    }

    [Fact]
    public void 停用项不加载()
    {
        var ids = Filter(192, 1, Aux(1, "X", 192, 1, enable: 0));
        Assert.Empty(ids);
    }
}
