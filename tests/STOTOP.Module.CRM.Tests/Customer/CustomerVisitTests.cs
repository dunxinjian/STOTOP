using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services;
using Xunit;

namespace STOTOP.Module.CRM.Tests.Customer;

// STOTOP.Module 下有 Task/System 子命名空间会与 System.Threading.Tasks.Task 撞名；
// 文件作用域命名空间「之后」用 global:: 别名消歧（泛型 Task<T> 不受影响）。
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// CustomerVisit 簇首批单测：CustomerService（需 fake）+ VisitRecordService（零 fake）。
/// 服务返回 DTO/bool/实体（非 ApiResult）；业务错误抛 InvalidOperationException。
/// </summary>
public class CustomerVisitTests
{
    // ---------- 构造帮手 ----------

    private static CustomerService CreateCustomerService(STOTOPDbContext db, CrmTestFakes.CountingEventDispatcher dispatcher)
        => new CustomerService(
            db,
            new Repository<CrmCustomer>(db),
            new Repository<CrmCustomerContact>(db),
            new Repository<CrmCustomerTransfer>(db),
            new Repository<CrmVisitRecord>(db),
            new Repository<CrmServiceOrder>(db),
            new Repository<CrmServiceFeedback>(db),
            new Repository<CrmReferral>(db),
            CrmTestFakes.Logger<CustomerService>(),
            dispatcher);

    private static VisitRecordService CreateVisitService(STOTOPDbContext db)
        => new VisitRecordService(
            new Repository<CrmVisitRecord>(db),
            new Repository<CrmCustomer>(db));

    /// <summary>直接落库一个客户（绕过服务），FOrgId 由保存时自动回填。</summary>
    private static async Task SeedCustomerAsync(STOTOPDbContext db, string code, string shortName = "测试客户", int status = 1)
    {
        db.Set<CrmCustomer>().Add(new CrmCustomer
        {
            FCode = code,
            FShortName = shortName,
            FStatus = status,
            FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
    }

    // ======================= CustomerService =======================

    [Fact]
    public async Task CreateCustomerAsync_编号重复时抛已存在()
    {
        await using var db = TestDbContextFactory.Create(nameof(CreateCustomerAsync_编号重复时抛已存在), orgId: 1);
        await SeedCustomerAsync(db, "C001");
        var svc = CreateCustomerService(db, new CrmTestFakes.CountingEventDispatcher());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateCustomerAsync(new CreateCustomerRequest { Code = "C001", ShortName = "重名编号" }));

        Assert.Contains("C001", ex.Message);
        Assert.Contains("已存在", ex.Message);
    }

    [Fact]
    public async Task CreateCustomerAsync_新建客户初始状态为0潜在()
    {
        await using var db = TestDbContextFactory.Create(nameof(CreateCustomerAsync_新建客户初始状态为0潜在), orgId: 1);
        var svc = CreateCustomerService(db, new CrmTestFakes.CountingEventDispatcher());

        var dto = await svc.CreateCustomerAsync(new CreateCustomerRequest { Code = "C100", ShortName = "新客户甲" });

        Assert.Equal("C100", dto.Code);
        Assert.Equal("新客户甲", dto.ShortName);
        Assert.Equal(0, dto.Status); // CreateCustomerAsync 显式置 0，覆盖实体默认 1
    }

    [Fact]
    public async Task UpdateStatusAsync_写入新状态并生成状态变更流转记录()
    {
        await using var db = TestDbContextFactory.Create(nameof(UpdateStatusAsync_写入新状态并生成状态变更流转记录), orgId: 1);
        await SeedCustomerAsync(db, "C200", status: 1);
        var svc = CreateCustomerService(db, new CrmTestFakes.CountingEventDispatcher());

        var ok = await svc.UpdateStatusAsync("C200", 2);

        Assert.True(ok);

        // 客户状态已落库
        var customer = await db.Set<CrmCustomer>().AsNoTracking().SingleAsync(c => c.FCode == "C200");
        Assert.Equal(2, customer.FStatus);

        // 生成一条 FTransferType=3 的状态变更流转，原状态 1 -> 新状态 2
        var transfer = await db.Set<CrmCustomerTransfer>().AsNoTracking()
            .SingleAsync(t => t.FCustomerId == "C200");
        Assert.Equal(3, transfer.FTransferType);
        Assert.Equal(1, transfer.FOriginalStatus);
        Assert.Equal(2, transfer.FNewStatus);
    }

    [Fact]
    public async Task UpdateStatusAsync_客户不存在返回false()
    {
        await using var db = TestDbContextFactory.Create(nameof(UpdateStatusAsync_客户不存在返回false), orgId: 1);
        var svc = CreateCustomerService(db, new CrmTestFakes.CountingEventDispatcher());

        var ok = await svc.UpdateStatusAsync("NOPE", 2);

        Assert.False(ok);
        Assert.Empty(await db.Set<CrmCustomerTransfer>().AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task UpdateCustomerAsync_简称变更触发一次辅助核算同步事件()
    {
        await using var db = TestDbContextFactory.Create(nameof(UpdateCustomerAsync_简称变更触发一次辅助核算同步事件), orgId: 1);
        await SeedCustomerAsync(db, "C300", shortName: "旧名");
        var dispatcher = new CrmTestFakes.CountingEventDispatcher();
        var svc = CreateCustomerService(db, dispatcher);

        // OrgId 显式给 1（与 seed 自动回填一致），仅改简称，隔离事件断言
        await svc.UpdateCustomerAsync("C300", new UpdateCustomerRequest { ShortName = "新名", OrgId = 1 });

        Assert.Equal(1, dispatcher.PublishCount);
        Assert.IsType<STOTOP.Infrastructure.Events.AuxiliarySourceChangedEvent>(dispatcher.LastEvent);
        var evt = (STOTOP.Infrastructure.Events.AuxiliarySourceChangedEvent)dispatcher.LastEvent!;
        Assert.Equal("新名", evt.NewName);
    }

    [Fact]
    public async Task UpdateCustomerAsync_简称不变不发布事件()
    {
        await using var db = TestDbContextFactory.Create(nameof(UpdateCustomerAsync_简称不变不发布事件), orgId: 1);
        await SeedCustomerAsync(db, "C310", shortName: "同名");
        var dispatcher = new CrmTestFakes.CountingEventDispatcher();
        var svc = CreateCustomerService(db, dispatcher);

        await svc.UpdateCustomerAsync("C310", new UpdateCustomerRequest { ShortName = "同名", OrgId = 1 });

        Assert.Equal(0, dispatcher.PublishCount);
    }

    [Fact]
    public async Task GetStatisticsAsync_按三态分组统计客户数()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetStatisticsAsync_按三态分组统计客户数), orgId: 1);
        await SeedCustomerAsync(db, "P1", status: 0); // 潜在
        await SeedCustomerAsync(db, "A1", status: 1); // 活跃
        await SeedCustomerAsync(db, "A2", status: 1); // 活跃
        await SeedCustomerAsync(db, "L1", status: 2); // 流失
        var svc = CreateCustomerService(db, new CrmTestFakes.CountingEventDispatcher());

        var stat = await svc.GetStatisticsAsync();

        Assert.Equal(4, stat.TotalCount);
        Assert.Equal(3, stat.ByStatus.Count); // 固定三态：潜在/活跃/流失
        Assert.Equal(1, stat.ByStatus.Single(b => b.Status == 0).Count);
        Assert.Equal(2, stat.ByStatus.Single(b => b.Status == 1).Count);
        Assert.Equal(1, stat.ByStatus.Single(b => b.Status == 2).Count);
        Assert.Equal("活跃", stat.ByStatus.Single(b => b.Status == 1).StatusName);
    }

    // ======================= VisitRecordService =======================

    [Fact]
    public async Task CreateVisitRecordAsync_客户不存在抛客户不存在()
    {
        await using var db = TestDbContextFactory.Create(nameof(CreateVisitRecordAsync_客户不存在抛客户不存在), orgId: 1);
        var svc = CreateVisitService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateVisitRecordAsync(new CreateVisitRecordRequest
            {
                CustomerId = "MISSING",
                VisitorId = 1,
                VisitDate = DateOnly.FromDateTime(DateTime.Today),
                VisitMethod = 2
            }));

        Assert.Contains("客户不存在", ex.Message);
    }

    [Fact]
    public async Task GetPendingFollowUpAsync_只返回到期且非空跟进日并按升序排列()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetPendingFollowUpAsync_只返回到期且非空跟进日并按升序排列), orgId: 1);
        await SeedCustomerAsync(db, "C400");
        var today = DateOnly.FromDateTime(DateTime.Today);

        // 三天前到期（应入选）
        db.Set<CrmVisitRecord>().Add(new CrmVisitRecord
        {
            FCustomerId = "C400", FVisitorId = 1,
            FVisitDate = today.AddDays(-5), FVisitMethod = 1,
            FNextFollowUpDate = today.AddDays(-3), FCreatedTime = DateTime.Now
        });
        // 今天到期（应入选）
        db.Set<CrmVisitRecord>().Add(new CrmVisitRecord
        {
            FCustomerId = "C400", FVisitorId = 1,
            FVisitDate = today.AddDays(-1), FVisitMethod = 1,
            FNextFollowUpDate = today, FCreatedTime = DateTime.Now
        });
        // 未来到期（不入选）
        db.Set<CrmVisitRecord>().Add(new CrmVisitRecord
        {
            FCustomerId = "C400", FVisitorId = 1,
            FVisitDate = today, FVisitMethod = 1,
            FNextFollowUpDate = today.AddDays(5), FCreatedTime = DateTime.Now
        });
        // 无跟进日（不入选）
        db.Set<CrmVisitRecord>().Add(new CrmVisitRecord
        {
            FCustomerId = "C400", FVisitorId = 1,
            FVisitDate = today, FVisitMethod = 1,
            FNextFollowUpDate = null, FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        var svc = CreateVisitService(db);

        var pending = await svc.GetPendingFollowUpAsync();

        Assert.Equal(2, pending.Count);
        // 升序：最早的（-3 天）在前
        Assert.Equal(today.AddDays(-3), pending[0].NextFollowUpDate);
        Assert.Equal(today, pending[1].NextFollowUpDate);
    }

    [Fact]
    public async Task GetStatisticsAsync_统计今日本周本月拜访数()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetStatisticsAsync_统计今日本周本月拜访数), orgId: 1);
        await SeedCustomerAsync(db, "C500");
        var today = DateOnly.FromDateTime(DateTime.Today);

        // 今日一条：同时计入 今日/本周/本月/总数
        db.Set<CrmVisitRecord>().Add(new CrmVisitRecord
        {
            FCustomerId = "C500", FVisitorId = 1,
            FVisitDate = today, FVisitMethod = 1, FCreatedTime = DateTime.Now
        });
        // 很久以前一条：只计入 总数（不在本周、不在本月）
        db.Set<CrmVisitRecord>().Add(new CrmVisitRecord
        {
            FCustomerId = "C500", FVisitorId = 1,
            FVisitDate = today.AddMonths(-2).AddDays(-3), FVisitMethod = 1, FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        var svc = CreateVisitService(db);

        var stat = await svc.GetStatisticsAsync();

        Assert.Equal(2, stat.TotalVisits);
        Assert.Equal(1, stat.TodayVisits);
        Assert.Equal(1, stat.WeekVisits);  // 仅今日那条落在本周
        Assert.Equal(1, stat.MonthVisits); // 仅今日那条落在本月
    }
}
