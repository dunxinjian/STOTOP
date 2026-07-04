using Microsoft.EntityFrameworkCore;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;
using STT = System.Threading.Tasks;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段4E R4 待办硬化自检：CF待办分发日志 + TodoDispatchLogService。
/// 回调据 taskId 权威绑定精确定位待办（替代尾缀模糊匹配，防伪造命中无关/跨租户待办）+ 同事件重放幂等。
/// CF待办分发日志 ITenantScoped，随 CardFlow 模块注册进 System.Tests 模型，InMemory 可验。
/// </summary>
public class TodoDispatchLogTests
{
    [Fact]
    public async STT.Task 记录分发_按待办加渠道幂等_更新taskId并清幂等标记()
    {
        using var ctx = TestDbContextFactory.Create("dispatchlog", tenantId: 1);
        var svc = new TodoDispatchLogService(ctx);

        await svc.RecordDispatchAsync(todoItemId: 10, tenantId: 1, channel: "dingtalk", externalTaskId: "T1", corpId: null);
        await svc.RecordDispatchAsync(todoItemId: 10, tenantId: 1, channel: "dingtalk", externalTaskId: "T2", corpId: null);

        var logs = ctx.Set<CfTodoDispatchLog>().IgnoreQueryFilters().Where(l => l.FTodoItemId == 10).ToList();
        Assert.Single(logs);                        // 幂等：同 待办+渠道 一条
        Assert.Equal("T2", logs[0].FExternalTaskId); // 重推更新 taskId
        Assert.Equal(1L, logs[0].FTenantId);
    }

    [Fact]
    public async STT.Task 回调据taskId定位待办_同事件重放幂等_异事件仍处理()
    {
        using var ctx = TestDbContextFactory.Create("dispatchlog", tenantId: 1);
        var svc = new TodoDispatchLogService(ctx);
        await svc.RecordDispatchAsync(20, 1, "dingtalk", "T20", null);

        var first = await svc.TryBeginCallbackAsync("T20", "completed");
        Assert.Equal(20L, first.TodoItemId);
        Assert.False(first.AlreadyProcessed);       // 首次处理

        var replay = await svc.TryBeginCallbackAsync("T20", "completed");
        Assert.Equal(20L, replay.TodoItemId);
        Assert.True(replay.AlreadyProcessed);        // 同事件重放 → 幂等跳过

        var otherEvent = await svc.TryBeginCallbackAsync("T20", "deleted");
        Assert.Equal(20L, otherEvent.TodoItemId);
        Assert.False(otherEvent.AlreadyProcessed);   // 不同事件仍处理
    }

    [Fact]
    public async STT.Task 回调无分发记录_返回null供调用方legacy兜底()
    {
        using var ctx = TestDbContextFactory.Create("dispatchlog", tenantId: 1);
        var svc = new TodoDispatchLogService(ctx);

        var r = await svc.TryBeginCallbackAsync("unknown-task", "completed");
        Assert.Null(r.TodoItemId);                   // 无分发记录（伪造/历史遗留）
        Assert.False(r.AlreadyProcessed);
    }
}
