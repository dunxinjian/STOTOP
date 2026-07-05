using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Services;
using Xunit;
// 注：STOTOP.Module.Task 命名空间会遮蔽 System.Threading.Tasks.Task（CS0118），故下方返回类型全限定

namespace STOTOP.Module.Express.Tests.Billing;

/// <summary>
/// P0：主政策返利分档口径必须随结算周期。日结逐日落档；周/月结按账期累计单量整体落档，
/// 避免申通"月累计单量"政策被逐日落到低档而系统性少算返利。
/// </summary>
public class PolicyRebateCycleTests
{
    // 档位：500-15000 → 1.15；>15000 → 1.35（累进制）
    private static PolicyRebateCalcEngine BuildEngine(int settlementCycle, out long policyId)
    {
        var db = TestDbContextFactory.Create(nameof(PolicyRebateCycleTests), orgId: 1);

        var policy = new ExpPolicyRebate
        {
            FID = 1,
            FOrgId = 1,
            FTenantId = 1,
            FBrandCode = "ST",
            FPolicyName = "测试月累计返利",
            FRebateMode = 2,               // 阶梯
            FSettlementCycle = settlementCycle,
            FStatus = 1
        };
        db.Set<ExpPolicyRebate>().Add(policy);
        db.Set<ExpPolicyRebateTier>().AddRange(
            new ExpPolicyRebateTier { FID = 1, FPolicyRebateId = 1, FDailyVolumeFrom = 500, FDailyVolumeTo = 15000, FRebatePerTicket = 1.15m, FSortOrder = 1 },
            new ExpPolicyRebateTier { FID = 2, FPolicyRebateId = 1, FDailyVolumeFrom = 15001, FDailyVolumeTo = null, FRebatePerTicket = 1.35m, FSortOrder = 2 });
        db.SaveChanges();

        policyId = 1;
        return new PolicyRebateCalcEngine(
            new Repository<ExpPolicyRebate>(db),
            new Repository<ExpPolicyRebateTier>(db),
            new Repository<ExpPolicyRebateRule>(db),
            new Repository<ExpPolicyRebateRuleItem>(db));
    }

    // 账期 20000 票，分 2 天各 10000（每日均低于 15000 档上限）
    private static List<DailyWaybillSummary> TwoDaysOf10000() =>
    [
        new() { Date = new DateTime(2026, 5, 1), Count = 10000 },
        new() { Date = new DateTime(2026, 5, 2), Count = 10000 }
    ];

    [Fact]
    public async global::System.Threading.Tasks.Task Monthly_cycle_tiers_on_period_total_not_per_day()
    {
        var engine = BuildEngine(settlementCycle: 3, out var policyId);

        var rebate = await engine.CalculateBaseRebateAsync(policyId, TwoDaysOf10000());

        // 账期累计 20000：14501×1.15 + 5499×1.35 = 16676.15 + 7423.65
        Assert.Equal(24099.80m, rebate);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Daily_cycle_tiers_per_day()
    {
        var engine = BuildEngine(settlementCycle: 1, out var policyId);

        var rebate = await engine.CalculateBaseRebateAsync(policyId, TwoDaysOf10000());

        // 逐日落档：每天 10000 全落首档 ×1.15 → 2×10000×1.15
        Assert.Equal(23000.00m, rebate);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Monthly_cycle_low_total_below_first_tier_yields_zero()
    {
        var engine = BuildEngine(settlementCycle: 3, out var policyId);

        // 账期累计 300（< 首档起点 500）→ 无返利
        var rebate = await engine.CalculateBaseRebateAsync(policyId,
            [new() { Date = new DateTime(2026, 5, 1), Count = 300 }]);

        Assert.Equal(0m, rebate);
    }
}
