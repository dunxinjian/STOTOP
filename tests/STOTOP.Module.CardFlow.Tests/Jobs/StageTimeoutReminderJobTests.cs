using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Core.Services;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Jobs;
using STOTOP.Module.CardFlow.Services.Interfaces;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Jobs;

/// <summary>
/// M2-7 StageTimeoutReminderJob：active 人工节点超过 FTimeoutHours 且 F超时提醒时间 为 null
/// → 未处理待办重推通知 + 置 FTimeoutRemindedAt；未超时/已提醒不动。
/// </summary>
public class StageTimeoutReminderJobTests
{
    private const long FlowVersionId = 3601;
    private const long StageDefId = 6401;

    [Fact]
    public async global::System.Threading.Tasks.Task 超时未提醒实例_被标记提醒并重推待办通知()
    {
        using var db = TestDbContextFactory.Create(nameof(超时未提醒实例_被标记提醒并重推待办通知));
        SeedStageDef(db, timeoutHours: 2);
        SeedInstance(db, cardId: 9771, stageInstanceId: 9871,
            activatedTime: DateTime.Now.AddHours(-3), remindedAt: null);
        db.Set<CfTodoItem>().Add(new CfTodoItem
        {
            FID = 9971, FCardId = 9771, FStageInstanceId = 9871, FHandlerId = 81,
            FHandlerName = "处理人", FStatus = "pending", FOrgId = 1
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var dispatcher = new RecordingNotificationDispatcher();
        var job = CreateJob(db, dispatcher);
        await job.ExecuteAsync();

        db.ChangeTracker.Clear();
        var instance = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == 9871);
        Assert.NotNull(instance.FTimeoutRemindedAt);
        Assert.Contains(9971L, dispatcher.CreatedTodoIds);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 未超时实例_不标记不提醒()
    {
        using var db = TestDbContextFactory.Create(nameof(未超时实例_不标记不提醒));
        SeedStageDef(db, timeoutHours: 24);
        SeedInstance(db, cardId: 9772, stageInstanceId: 9872,
            activatedTime: DateTime.Now.AddHours(-1), remindedAt: null);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var dispatcher = new RecordingNotificationDispatcher();
        var job = CreateJob(db, dispatcher);
        await job.ExecuteAsync();

        db.ChangeTracker.Clear();
        var instance = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == 9872);
        Assert.Null(instance.FTimeoutRemindedAt);
        Assert.Empty(dispatcher.CreatedTodoIds);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 已提醒实例_不重复提醒()
    {
        using var db = TestDbContextFactory.Create(nameof(已提醒实例_不重复提醒));
        SeedStageDef(db, timeoutHours: 2);
        var firstRemindedAt = DateTime.Now.AddHours(-1);
        SeedInstance(db, cardId: 9773, stageInstanceId: 9873,
            activatedTime: DateTime.Now.AddHours(-5), remindedAt: firstRemindedAt);
        db.Set<CfTodoItem>().Add(new CfTodoItem
        {
            FID = 9973, FCardId = 9773, FStageInstanceId = 9873, FHandlerId = 81,
            FHandlerName = "处理人", FStatus = "pending", FOrgId = 1
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var dispatcher = new RecordingNotificationDispatcher();
        var job = CreateJob(db, dispatcher);
        await job.ExecuteAsync();

        db.ChangeTracker.Clear();
        var instance = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == 9873);
        // 提醒时间不被覆盖，且不再重推通知
        Assert.Equal(firstRemindedAt, instance.FTimeoutRemindedAt!.Value, TimeSpan.FromSeconds(1));
        Assert.Empty(dispatcher.CreatedTodoIds);
    }

    private static void SeedStageDef(STOTOP.Infrastructure.Data.STOTOPDbContext db, int timeoutHours)
    {
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = StageDefId, FFlowVersionId = FlowVersionId, FSortOrder = 1, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FTimeoutHours = timeoutHours
        });
    }

    private static void SeedInstance(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, long cardId, long stageInstanceId,
        DateTime activatedTime, DateTime? remindedAt)
    {
        db.Set<CfCard>().Add(new CfCard
        {
            FID = cardId, FFlowDefinitionId = 1, FFlowVersionId = FlowVersionId,
            FTitle = "超时提醒用例", FStatus = "active", FInitiatorId = 1, FInitiatorName = "发起人",
            FCurrentStageInstanceId = stageInstanceId, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = stageInstanceId, FCardId = cardId, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active",
            FActivatedTime = activatedTime, FStartTime = activatedTime, FTimeoutRemindedAt = remindedAt
        });
    }

    private static StageTimeoutReminderJob CreateJob(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, INotificationDispatcher dispatcher)
    {
        return new StageTimeoutReminderJob(
            db,
            dispatcher,
            new SingleTenantIterationFake(),
            NullLogger<StageTimeoutReminderJob>.Instance);
    }

    /// <summary>InMemory 测试无租户过滤器语义，直接以单租户调用一次 action。</summary>
    private sealed class SingleTenantIterationFake : ITenantIterationService
    {
        public global::System.Threading.Tasks.Task ForEachActiveTenantAsync(
            Func<long, global::System.Threading.Tasks.Task> action, string reason = "tenant-iteration")
            => action(1);
    }

    private sealed class RecordingNotificationDispatcher : INotificationDispatcher
    {
        public List<long> CreatedTodoIds { get; } = new();

        public global::System.Threading.Tasks.Task DispatchCreateTodoAsync(long todoItemId)
        {
            CreatedTodoIds.Add(todoItemId);
            return global::System.Threading.Tasks.Task.CompletedTask;
        }

        public global::System.Threading.Tasks.Task DispatchCompleteTodoAsync(long todoItemId) => global::System.Threading.Tasks.Task.CompletedTask;
        public global::System.Threading.Tasks.Task DispatchDeleteTodoAsync(long todoItemId) => global::System.Threading.Tasks.Task.CompletedTask;
        public global::System.Threading.Tasks.Task RetryPushAsync(long todoItemId) => global::System.Threading.Tasks.Task.CompletedTask;
    }
}
