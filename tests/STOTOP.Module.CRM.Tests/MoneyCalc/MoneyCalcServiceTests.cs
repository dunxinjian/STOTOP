using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services;
using Xunit;

namespace STOTOP.Module.CRM.Tests.MoneyCalc;

// STOTOP.Module 下同时存在 Task / System 子命名空间，会与 System.Threading.Tasks.Task 撞名；
// 文件作用域命名空间之后用 global:: 别名消除歧义（泛型 Task<T> 不受影响）。
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// MoneyCalc 簇首批单元测试：聚焦零 fake 的金额计算确定性行为。
/// 覆盖 ProfitCalcService（毛利/毛利率/数据来源、零收入避免除零、重算、聚合汇总、客户排名）
/// 与 ReferralCommissionService（返佣金额 = 期间收入合计 × 比例、缺比例/缺推荐报错、审批回调状态机、提交审批废弃路径）。
/// 服务返回 DTO/实体/void，业务错误抛 InvalidOperationException；不存在 ApiResult.Code 约定。
/// </summary>
public class MoneyCalcServiceTests
{
    private static ProfitCalcService NewProfitService(STOTOPDbContext db)
        => new ProfitCalcService(new Repository<CrmCustomerProfit>(db), new Repository<CrmCustomer>(db));

    private static ReferralCommissionService NewCommissionService(STOTOPDbContext db)
        => new ReferralCommissionService(
            new Repository<CrmExternalContact>(db),
            new Repository<CrmReferral>(db),
            new Repository<CrmCommission>(db),
            new Repository<CrmCustomer>(db),
            new Repository<CrmCustomerProfit>(db),
            db);

    /// <summary>种子一个客户（FCode 为主键，需与利润/推荐的 FCustomerId 对应）。</summary>
    private static async Task SeedCustomerAsync(STOTOPDbContext db, string code, string shortName)
    {
        db.Set<CrmCustomer>().Add(new CrmCustomer { FCode = code, FShortName = shortName });
        await db.SaveChangesAsync();
    }

    // ===== ProfitCalcService =====

    [Fact]
    public async Task 创建毛利按收入减成本与毛利率计算并标记手动来源()
    {
        await using var db = TestDbContextFactory.Create(nameof(创建毛利按收入减成本与毛利率计算并标记手动来源), orgId: 1);
        await SeedCustomerAsync(db, "C1", "客户一");
        var svc = NewProfitService(db);

        var dto = await svc.CreateProfitAsync(new CreateProfitRequest
        {
            CustomerId = "C1",
            Period = "2026-01",
            Revenue = 1000m,
            Cost = 300m
        });

        Assert.Equal(700m, dto.Profit);
        Assert.Equal(70.00m, dto.ProfitRate);
        Assert.Equal(2, dto.DataSource); // 2=手动录入
        Assert.Equal("C1", dto.CustomerId);
    }

    [Fact]
    public async Task 创建毛利收入为零时毛利率为零避免除零()
    {
        await using var db = TestDbContextFactory.Create(nameof(创建毛利收入为零时毛利率为零避免除零), orgId: 1);
        await SeedCustomerAsync(db, "C1", "客户一");
        var svc = NewProfitService(db);

        var dto = await svc.CreateProfitAsync(new CreateProfitRequest
        {
            CustomerId = "C1",
            Period = "2026-01",
            Revenue = 0m,
            Cost = 500m
        });

        Assert.Equal(-500m, dto.Profit);
        Assert.Equal(0m, dto.ProfitRate);
    }

    [Fact]
    public async Task 更新毛利重算毛利与毛利率()
    {
        await using var db = TestDbContextFactory.Create(nameof(更新毛利重算毛利与毛利率), orgId: 1);
        await SeedCustomerAsync(db, "C1", "客户一");
        var svc = NewProfitService(db);

        var created = await svc.CreateProfitAsync(new CreateProfitRequest
        {
            CustomerId = "C1",
            OrgId = 1,
            Period = "2026-01",
            Revenue = 1000m,
            Cost = 300m
        });

        var updated = await svc.UpdateProfitAsync(created.Id, new CreateProfitRequest
        {
            CustomerId = "C1",
            OrgId = 1, // 必须回带组织：UpdateProfitAsync 以 request.OrgId??0 覆写 FOrgId，置 0 会被组织查询过滤器排除导致回查为 null
            Period = "2026-01",
            Revenue = 800m,
            Cost = 200m
        });

        Assert.NotNull(updated);
        Assert.Equal(600m, updated!.Profit);
        Assert.Equal(75.00m, updated.ProfitRate);
    }

    [Fact]
    public async Task 毛利汇总按组织期间聚合收入成本毛利并去重客户数()
    {
        await using var db = TestDbContextFactory.Create(nameof(毛利汇总按组织期间聚合收入成本毛利并去重客户数), orgId: 1);
        await SeedCustomerAsync(db, "C1", "客户一");
        await SeedCustomerAsync(db, "C2", "客户二");
        var svc = NewProfitService(db);

        // 同期间内：C1 出现两条、C2 一条 -> CustomerCount 去重应为 2
        await svc.CreateProfitAsync(new CreateProfitRequest { CustomerId = "C1", Period = "2026-01", Revenue = 1000m, Cost = 400m });
        await svc.CreateProfitAsync(new CreateProfitRequest { CustomerId = "C1", Period = "2026-01", Revenue = 500m, Cost = 100m });
        await svc.CreateProfitAsync(new CreateProfitRequest { CustomerId = "C2", Period = "2026-01", Revenue = 2000m, Cost = 1000m });

        var summary = await svc.GetProfitSummaryAsync(orgId: 1, period: "2026-01");

        var row = Assert.Single(summary);
        Assert.Equal("2026-01", row.Period);
        Assert.Equal(3500m, row.TotalRevenue);  // 1000+500+2000
        Assert.Equal(1500m, row.TotalCost);     // 400+100+1000
        Assert.Equal(2000m, row.TotalProfit);   // 600+400+1000
        Assert.Equal(2, row.CustomerCount);     // C1、C2
    }

    [Fact]
    public async Task 毛利排名按客户毛利合计降序并截断TopN()
    {
        await using var db = TestDbContextFactory.Create(nameof(毛利排名按客户毛利合计降序并截断TopN), orgId: 1);
        await SeedCustomerAsync(db, "C1", "客户一");
        await SeedCustomerAsync(db, "C2", "客户二");
        await SeedCustomerAsync(db, "C3", "客户三");
        var svc = NewProfitService(db);

        await svc.CreateProfitAsync(new CreateProfitRequest { CustomerId = "C1", Period = "2026-01", Revenue = 1000m, Cost = 900m }); // 毛利 100
        await svc.CreateProfitAsync(new CreateProfitRequest { CustomerId = "C2", Period = "2026-01", Revenue = 1000m, Cost = 100m }); // 毛利 900
        await svc.CreateProfitAsync(new CreateProfitRequest { CustomerId = "C3", Period = "2026-01", Revenue = 1000m, Cost = 500m }); // 毛利 500

        var ranking = await svc.GetProfitRankingAsync(orgId: 1, period: "2026-01", top: 2);

        Assert.Equal(2, ranking.Count); // 截断 Top2
        Assert.Equal("C2", ranking[0].CustomerId); // 毛利最高
        Assert.Equal(900m, ranking[0].TotalProfit);
        Assert.Equal("C3", ranking[1].CustomerId);
        Assert.Equal("客户二", ranking[0].CustomerName); // 按 Customer.FShortName 关联分组
    }

    // ===== ReferralCommissionService =====

    [Fact]
    public async Task 计算返佣等于期间收入合计乘以比例并按两位四舍五入()
    {
        await using var db = TestDbContextFactory.Create(nameof(计算返佣等于期间收入合计乘以比例并按两位四舍五入), orgId: 1);
        await SeedCustomerAsync(db, "C1", "客户一");

        var profitRepo = new Repository<CrmCustomerProfit>(db);
        // 期间 2026-01..2026-03 内三条收入：1000 + 2000 + 3000 = 6000；另有期间外一条不计入
        await profitRepo.AddAsync(new CrmCustomerProfit { FCustomerId = "C1", FPeriod = "2026-01", FRevenue = 1000m });
        await profitRepo.AddAsync(new CrmCustomerProfit { FCustomerId = "C1", FPeriod = "2026-02", FRevenue = 2000m });
        await profitRepo.AddAsync(new CrmCustomerProfit { FCustomerId = "C1", FPeriod = "2026-03", FRevenue = 3000m });
        await profitRepo.AddAsync(new CrmCustomerProfit { FCustomerId = "C1", FPeriod = "2026-04", FRevenue = 9999m });

        var referralRepo = new Repository<CrmReferral>(db);
        var referral = await referralRepo.AddAsync(new CrmReferral
        {
            FCustomerId = "C1",
            FReferrerType = 2,
            FReferralDate = new DateOnly(2026, 1, 1),
            FCommissionRate = 5m // 5%
        });

        var svc = NewCommissionService(db);
        var result = await svc.CalcCommissionAsync(new CalcCommissionRequest
        {
            ReferralId = referral.FID,
            CustomerId = "C1",
            StartPeriod = "2026-01",
            EndPeriod = "2026-03"
        });

        Assert.Equal(6000m, result.TotalRevenue);
        Assert.Equal(300.00m, result.CalcAmount); // 6000 * 5 / 100
        Assert.Equal(5m, result.CommissionRate);
        Assert.Equal("客户一", result.CustomerName);
    }

    [Fact]
    public async Task 计算返佣推荐记录不存在时抛业务异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(计算返佣推荐记录不存在时抛业务异常), orgId: 1);
        var svc = NewCommissionService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CalcCommissionAsync(new CalcCommissionRequest
            {
                ReferralId = 999999,
                CustomerId = "C1",
                StartPeriod = "2026-01",
                EndPeriod = "2026-03"
            }));

        Assert.Contains("推荐记录不存在", ex.Message);
    }

    [Fact]
    public async Task 计算返佣未设置返佣比例时抛业务异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(计算返佣未设置返佣比例时抛业务异常), orgId: 1);
        await SeedCustomerAsync(db, "C1", "客户一");

        var referralRepo = new Repository<CrmReferral>(db);
        var referral = await referralRepo.AddAsync(new CrmReferral
        {
            FCustomerId = "C1",
            FReferrerType = 2,
            FReferralDate = new DateOnly(2026, 1, 1),
            FCommissionRate = null // 未设置比例
        });

        var svc = NewCommissionService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CalcCommissionAsync(new CalcCommissionRequest
            {
                ReferralId = referral.FID,
                CustomerId = "C1",
                StartPeriod = "2026-01",
                EndPeriod = "2026-03"
            }));

        Assert.Contains("未设置返佣比例", ex.Message);
    }

    [Fact]
    public async Task 审批回调通过将审批中置为已批准()
    {
        await using var db = TestDbContextFactory.Create(nameof(审批回调通过将审批中置为已批准), orgId: 1);
        var commissionRepo = new Repository<CrmCommission>(db);
        var commission = await commissionRepo.AddAsync(new CrmCommission
        {
            FReferralId = 1,
            FCustomerId = "C1",
            FCommissionAmount = 100m,
            FStatus = 1 // 审批中
        });

        var svc = NewCommissionService(db);
        await svc.HandleApprovalCallbackAsync(new ApprovalCallbackRequest { CommissionId = commission.FID, Approved = true });

        var reloaded = await db.Set<CrmCommission>().AsNoTracking().FirstAsync(c => c.FID == commission.FID);
        Assert.Equal(2, reloaded.FStatus); // 2=已批准
    }

    [Fact]
    public async Task 审批回调驳回将审批中置为已驳回()
    {
        await using var db = TestDbContextFactory.Create(nameof(审批回调驳回将审批中置为已驳回), orgId: 1);
        var commissionRepo = new Repository<CrmCommission>(db);
        var commission = await commissionRepo.AddAsync(new CrmCommission
        {
            FReferralId = 1,
            FCustomerId = "C1",
            FCommissionAmount = 100m,
            FStatus = 1 // 审批中
        });

        var svc = NewCommissionService(db);
        await svc.HandleApprovalCallbackAsync(new ApprovalCallbackRequest { CommissionId = commission.FID, Approved = false });

        var reloaded = await db.Set<CrmCommission>().AsNoTracking().FirstAsync(c => c.FID == commission.FID);
        Assert.Equal(4, reloaded.FStatus); // 4=已驳回
    }

    [Fact]
    public async Task 审批回调非审批中状态抛业务异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(审批回调非审批中状态抛业务异常), orgId: 1);
        var commissionRepo = new Repository<CrmCommission>(db);
        var commission = await commissionRepo.AddAsync(new CrmCommission
        {
            FReferralId = 1,
            FCustomerId = "C1",
            FCommissionAmount = 100m,
            FStatus = 0 // 草稿，非审批中
        });

        var svc = NewCommissionService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.HandleApprovalCallbackAsync(new ApprovalCallbackRequest { CommissionId = commission.FID, Approved = true }));

        Assert.Contains("只有审批中", ex.Message);
    }

    [Fact]
    public async Task 提交审批非草稿状态抛业务异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(提交审批非草稿状态抛业务异常), orgId: 1);
        var commissionRepo = new Repository<CrmCommission>(db);
        var commission = await commissionRepo.AddAsync(new CrmCommission
        {
            FReferralId = 1,
            FCustomerId = "C1",
            FCommissionAmount = 100m,
            FStatus = 1 // 非草稿
        });

        var svc = NewCommissionService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SubmitForApprovalAsync(new SubmitCommissionRequest { CommissionId = commission.FID, OrgId = 1 }, userId: 1));

        Assert.Contains("只有草稿状态", ex.Message);
    }

    [Fact]
    public async Task 提交审批草稿状态走BPM已废除抛不支持()
    {
        await using var db = TestDbContextFactory.Create(nameof(提交审批草稿状态走BPM已废除抛不支持), orgId: 1);
        var commissionRepo = new Repository<CrmCommission>(db);
        var commission = await commissionRepo.AddAsync(new CrmCommission
        {
            FReferralId = 1,
            FCustomerId = "C1",
            FCommissionAmount = 100m,
            FStatus = 0 // 草稿
        });

        var svc = NewCommissionService(db);
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            svc.SubmitForApprovalAsync(new SubmitCommissionRequest { CommissionId = commission.FID, OrgId = 1 }, userId: 1));

        Assert.Contains("BPM流程已废除", ex.Message);
    }
}
