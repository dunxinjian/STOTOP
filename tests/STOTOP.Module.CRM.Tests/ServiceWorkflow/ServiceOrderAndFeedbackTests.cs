using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Repositories;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services;
using Xunit;

namespace STOTOP.Module.CRM.Tests.ServiceWorkflow;

// STOTOP.Module 下有 Task/System 子命名空间，会与 System.Threading.Tasks.Task 撞名；
// 在文件作用域命名空间「之后」用 global:: 别名消除歧义。
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// ServiceWorkflow 簇：两个纯状态机服务的单元测试（零 fake，仅 InMemory 仓储 + 组织上下文）。
/// 覆盖 ServiceOrderService 的接单/完成/转派/未知操作/创建/更新/统计，
/// 与 ServiceFeedbackService 的合法/非法状态流转/更新限制/查询分页。
/// </summary>
public class ServiceOrderAndFeedbackTests
{
    // ===== ServiceOrderService =====

    [Fact]
    public async Task 接单仅在待接单状态成功且置为处理中并回填受理人()
    {
        await using var db = TestDbContextFactory.Create(nameof(接单仅在待接单状态成功且置为处理中并回填受理人), orgId: 1);
        var orderRepo = new Repository<CrmServiceOrder>(db);
        var svc = new ServiceOrderService(orderRepo, new Repository<CrmServiceOrderLog>(db), new Repository<CrmCustomer>(db));

        var order = new CrmServiceOrder { FOrderNo = "SO1", FCustomerId = "C1", FTitle = "t", FStatus = 0 };
        await orderRepo.AddAsync(order);

        var ok = await svc.ExecuteActionAsync(order.FID, new ServiceOrderActionRequest { OperationType = 1, OperatorId = 77 });

        Assert.True(ok);
        var reloaded = await db.Set<CrmServiceOrder>().AsNoTracking().FirstAsync(o => o.FID == order.FID);
        Assert.Equal(1, reloaded.FStatus);
        Assert.Equal(77, reloaded.FAssigneeId);
    }

    [Fact]
    public async Task 非待接单状态接单抛业务异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(非待接单状态接单抛业务异常), orgId: 1);
        var orderRepo = new Repository<CrmServiceOrder>(db);
        var svc = new ServiceOrderService(orderRepo, new Repository<CrmServiceOrderLog>(db), new Repository<CrmCustomer>(db));

        var order = new CrmServiceOrder { FOrderNo = "SO1", FCustomerId = "C1", FTitle = "t", FStatus = 1 };
        await orderRepo.AddAsync(order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ExecuteActionAsync(order.FID, new ServiceOrderActionRequest { OperationType = 1, OperatorId = 1 }));
        Assert.Contains("待接单", ex.Message);
    }

    [Fact]
    public async Task 处理完成仅在处理中状态成功且置待确认并记录解决时间()
    {
        await using var db = TestDbContextFactory.Create(nameof(处理完成仅在处理中状态成功且置待确认并记录解决时间), orgId: 1);
        var orderRepo = new Repository<CrmServiceOrder>(db);
        var svc = new ServiceOrderService(orderRepo, new Repository<CrmServiceOrderLog>(db), new Repository<CrmCustomer>(db));

        var order = new CrmServiceOrder { FOrderNo = "SO1", FCustomerId = "C1", FTitle = "t", FStatus = 1 };
        await orderRepo.AddAsync(order);

        var ok = await svc.ExecuteActionAsync(order.FID, new ServiceOrderActionRequest { OperationType = 2, OperatorId = 5 });

        Assert.True(ok);
        var reloaded = await db.Set<CrmServiceOrder>().AsNoTracking().FirstAsync(o => o.FID == order.FID);
        Assert.Equal(2, reloaded.FStatus);
        Assert.NotNull(reloaded.FResolvedTime);
    }

    [Fact]
    public async Task 转派缺目标人员抛异常而指定目标后受理人改写且回到处理中()
    {
        await using var db = TestDbContextFactory.Create(nameof(转派缺目标人员抛异常而指定目标后受理人改写且回到处理中), orgId: 1);
        var orderRepo = new Repository<CrmServiceOrder>(db);
        var svc = new ServiceOrderService(orderRepo, new Repository<CrmServiceOrderLog>(db), new Repository<CrmCustomer>(db));

        var order = new CrmServiceOrder { FOrderNo = "SO1", FCustomerId = "C1", FTitle = "t", FStatus = 2, FAssigneeId = 10 };
        await orderRepo.AddAsync(order);

        // 无 TransferToId -> 抛异常
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ExecuteActionAsync(order.FID, new ServiceOrderActionRequest { OperationType = 3, OperatorId = 1 }));
        Assert.Contains("转派必须指定目标人员", ex.Message);

        // 指定 TransferToId -> 成功改写受理人、状态回到处理中(1)
        var ok = await svc.ExecuteActionAsync(order.FID, new ServiceOrderActionRequest { OperationType = 3, OperatorId = 1, TransferToId = 99 });
        Assert.True(ok);
        var reloaded = await db.Set<CrmServiceOrder>().AsNoTracking().FirstAsync(o => o.FID == order.FID);
        Assert.Equal(99, reloaded.FAssigneeId);
        Assert.Equal(1, reloaded.FStatus);
    }

    [Fact]
    public async Task 已完成工单不能转派抛异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(已完成工单不能转派抛异常), orgId: 1);
        var orderRepo = new Repository<CrmServiceOrder>(db);
        var svc = new ServiceOrderService(orderRepo, new Repository<CrmServiceOrderLog>(db), new Repository<CrmCustomer>(db));

        var order = new CrmServiceOrder { FOrderNo = "SO1", FCustomerId = "C1", FTitle = "t", FStatus = 3 };
        await orderRepo.AddAsync(order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ExecuteActionAsync(order.FID, new ServiceOrderActionRequest { OperationType = 3, OperatorId = 1, TransferToId = 99 }));
        Assert.Contains("不能转派", ex.Message);
    }

    [Fact]
    public async Task 未知操作类型抛不支持的操作类型()
    {
        await using var db = TestDbContextFactory.Create(nameof(未知操作类型抛不支持的操作类型), orgId: 1);
        var orderRepo = new Repository<CrmServiceOrder>(db);
        var svc = new ServiceOrderService(orderRepo, new Repository<CrmServiceOrderLog>(db), new Repository<CrmCustomer>(db));

        var order = new CrmServiceOrder { FOrderNo = "SO1", FCustomerId = "C1", FTitle = "t", FStatus = 0 };
        await orderRepo.AddAsync(order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ExecuteActionAsync(order.FID, new ServiceOrderActionRequest { OperationType = 99, OperatorId = 1 }));
        Assert.Contains("不支持的操作类型", ex.Message);
    }

    [Fact]
    public async Task 创建工单客户不存在抛异常_存在则工单号SO开头状态待接单且写日志()
    {
        await using var db = TestDbContextFactory.Create(nameof(创建工单客户不存在抛异常_存在则工单号SO开头状态待接单且写日志), orgId: 1);
        var orderRepo = new Repository<CrmServiceOrder>(db);
        var logRepo = new Repository<CrmServiceOrderLog>(db);
        var customerRepo = new Repository<CrmCustomer>(db);
        var svc = new ServiceOrderService(orderRepo, logRepo, customerRepo);

        // 客户不存在 -> 抛异常
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateServiceOrderAsync(new CreateServiceOrderRequest { CustomerId = "C-NONE", Title = "t" }));
        Assert.Contains("客户不存在", ex.Message);

        // 客户存在（服务按 FCode 匹配 request.CustomerId）-> 成功
        await customerRepo.AddAsync(new CrmCustomer { FCode = "C1", FShortName = "客户甲" });
        var dto = await svc.CreateServiceOrderAsync(new CreateServiceOrderRequest { CustomerId = "C1", Title = "标题", Category = 1, Priority = 2 });

        Assert.StartsWith("SO", dto.OrderNo);
        Assert.Equal(0, dto.Status);
        Assert.Equal("C1", dto.CustomerId);
        // 创建时写入一条日志
        var logCount = await db.Set<CrmServiceOrderLog>().AsNoTracking().CountAsync(l => l.FOrderId == dto.Id);
        Assert.Equal(1, logCount);
    }

    [Fact]
    public async Task 更新已完成或已关闭工单抛异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(更新已完成或已关闭工单抛异常), orgId: 1);
        var orderRepo = new Repository<CrmServiceOrder>(db);
        var svc = new ServiceOrderService(orderRepo, new Repository<CrmServiceOrderLog>(db), new Repository<CrmCustomer>(db));

        var order = new CrmServiceOrder { FOrderNo = "SO1", FCustomerId = "C1", FTitle = "t", FStatus = 3 };
        await orderRepo.AddAsync(order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateServiceOrderAsync(order.FID, new UpdateServiceOrderRequest { Title = "新", Category = 1, Priority = 2 }));
        Assert.Contains("不允许修改", ex.Message);
    }

    [Fact]
    public async Task 工单统计按五状态分别计数()
    {
        await using var db = TestDbContextFactory.Create(nameof(工单统计按五状态分别计数), orgId: 1);
        var orderRepo = new Repository<CrmServiceOrder>(db);
        var svc = new ServiceOrderService(orderRepo, new Repository<CrmServiceOrderLog>(db), new Repository<CrmCustomer>(db));

        // 状态分布：0×2, 1×1, 2×1, 3×3, 4×1 => Total=8
        var statuses = new[] { 0, 0, 1, 2, 3, 3, 3, 4 };
        var i = 0;
        foreach (var s in statuses)
            await orderRepo.AddAsync(new CrmServiceOrder { FOrderNo = $"SO{i++}", FCustomerId = "C1", FTitle = "t", FStatus = s });

        var stat = await svc.GetStatisticsAsync();

        Assert.Equal(8, stat.Total);
        Assert.Equal(2, stat.Pending);
        Assert.Equal(1, stat.Processing);
        Assert.Equal(1, stat.WaitingConfirm);
        Assert.Equal(3, stat.Completed);
        Assert.Equal(1, stat.Closed);
    }

    // ===== ServiceFeedbackService =====

    [Fact]
    public async Task 反馈合法流转成功写状态处理人与处理结果()
    {
        await using var db = TestDbContextFactory.Create(nameof(反馈合法流转成功写状态处理人与处理结果), orgId: 1);
        var repo = new Repository<CrmServiceFeedback>(db);
        var svc = new ServiceFeedbackService(repo);

        var fb = new CrmServiceFeedback { FTitle = "t", FStatus = 0 };
        await repo.AddAsync(fb);

        // 0 -> 1 合法
        var ok = await svc.HandleFeedbackAsync(fb.FID, new HandleFeedbackRequest { NewStatus = 1, HandlerId = 42, HandleResult = "已受理" });

        Assert.True(ok);
        var reloaded = await db.Set<CrmServiceFeedback>().AsNoTracking().FirstAsync(f => f.FID == fb.FID);
        Assert.Equal(1, reloaded.FStatus);
        Assert.Equal(42, reloaded.FHandlerId);
        Assert.Equal("已受理", reloaded.FHandleResult);
    }

    [Fact]
    public async Task 反馈非法流转抛异常_待审阅不可直达已落实()
    {
        await using var db = TestDbContextFactory.Create(nameof(反馈非法流转抛异常_待审阅不可直达已落实), orgId: 1);
        var repo = new Repository<CrmServiceFeedback>(db);
        var svc = new ServiceFeedbackService(repo);

        var fb = new CrmServiceFeedback { FTitle = "t", FStatus = 0 };
        await repo.AddAsync(fb);

        // 0 -> 3 非法
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.HandleFeedbackAsync(fb.FID, new HandleFeedbackRequest { NewStatus = 3, HandlerId = 1 }));
        Assert.Contains("不允许从状态", ex.Message);
    }

    [Fact]
    public async Task 反馈终态再流转抛异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(反馈终态再流转抛异常), orgId: 1);
        var repo = new Repository<CrmServiceFeedback>(db);
        var svc = new ServiceFeedbackService(repo);

        var fb = new CrmServiceFeedback { FTitle = "t", FStatus = 3 }; // 已落实，终态
        await repo.AddAsync(fb);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.HandleFeedbackAsync(fb.FID, new HandleFeedbackRequest { NewStatus = 4, HandlerId = 1 }));
        Assert.Contains("不允许从状态", ex.Message);
    }

    [Fact]
    public async Task 更新反馈在改善中及之后状态抛异常()
    {
        await using var db = TestDbContextFactory.Create(nameof(更新反馈在改善中及之后状态抛异常), orgId: 1);
        var repo = new Repository<CrmServiceFeedback>(db);
        var svc = new ServiceFeedbackService(repo);

        var fb = new CrmServiceFeedback { FTitle = "t", FStatus = 2 }; // 改善中 (>1)
        await repo.AddAsync(fb);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateFeedbackAsync(fb.FID, new UpdateServiceFeedbackRequest { Title = "新", Category = 1 }));
        Assert.Contains("当前状态不允许修改反馈信息", ex.Message);
    }

    [Fact]
    public async Task 反馈查询按客户与状态过滤并分页()
    {
        await using var db = TestDbContextFactory.Create(nameof(反馈查询按客户与状态过滤并分页), orgId: 1);
        var repo = new Repository<CrmServiceFeedback>(db);
        var svc = new ServiceFeedbackService(repo);

        // C1 + 状态0 共 3 条；另有干扰数据
        for (var k = 0; k < 3; k++)
            await repo.AddAsync(new CrmServiceFeedback { FTitle = $"a{k}", FCustomerId = "C1", FStatus = 0 });
        await repo.AddAsync(new CrmServiceFeedback { FTitle = "other-cust", FCustomerId = "C2", FStatus = 0 });
        await repo.AddAsync(new CrmServiceFeedback { FTitle = "other-status", FCustomerId = "C1", FStatus = 1 });

        var page = await svc.GetFeedbacksAsync(new ServiceFeedbackQueryRequest
        {
            CustomerId = "C1",
            Status = 0,
            PageIndex = 1,
            PageSize = 2
        });

        Assert.Equal(3, page.Total);          // 命中总数（过滤后）
        Assert.Equal(2, page.Items.Count);    // 首页按 PageSize 截断
    }
}
