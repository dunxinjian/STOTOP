using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Hubs;
using STOTOP.Module.CardFlow.Jobs;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.CardFlow.Tests.Approval;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Jobs;

/// <summary>
/// M8-C 件①：超时升级链（remind 既有行为不变 / autoApprove / autoReject / escalate）+ FTimeoutActionLevel 幂等高水位标记。
/// 复用 ApprovalRatioTests 的 CreateEngine 骨架（真实 FlowEngineService + 跨切面 Fake），确保升级动作经由
/// 真实引擎事务流转（非仅打桩断言调用）。IHubContext&lt;CardFlowHub&gt; 无既有 Fake，本文件手写最小实现。
/// </summary>
public class TimeoutEscalationTests
{
    private const long FlowDefId = 3500;
    private const long FlowVersionId = 3501;
    private const long ApproverId = 71;
    private const long SuperiorId = 72;
    private const long InitiatorId = 88;

    [Fact]
    public async global::System.Threading.Tasks.Task 超时达2倍_autoApprove动作_节点完成卡片完成()
    {
        const long stageDefId = 6501;
        const long cardId = 9601;
        const long stageInstanceId = 9701;
        using var db = TestDbContextFactory.Create(nameof(超时达2倍_autoApprove动作_节点完成卡片完成));
        await SeedSingleAssigneeStageAsync(db, stageDefId, timeoutHours: 2,
            timeoutActionJson: """{"levels":[{"multiplier":2,"action":"autoApprove"}]}""",
            cardId: cardId, stageInstanceId: stageInstanceId,
            activatedTime: DateTime.Now.AddHours(-5), assigneeFid: 9801);
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var job = CreateJob(db, engine, new FakeNotificationDispatcher());
        await job.ExecuteAsync();

        db.ChangeTracker.Clear();
        var stage = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == stageInstanceId);
        Assert.Equal("completed", stage.FStatus);
        Assert.Equal("approved", stage.FFinalAction);
        Assert.Equal(2, stage.FTimeoutActionLevel);
        Assert.True(stage.FIsTimeout); // 一次性标记路径与升级链共存，未被替代

        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == cardId);
        Assert.Equal("completed", card.FStatus); // 单节点、无下一节点 → 卡片整体完成

        var assignee = await db.Set<CfStageAssignee>().AsNoTracking().SingleAsync(a => a.FStageInstanceId == stageInstanceId);
        Assert.Equal("approved", assignee.FStatus);

        var log = await db.Set<CfActionLog>().AsNoTracking()
            .SingleAsync(l => l.FStageInstanceId == stageInstanceId && l.FActionType == "autoApprove");
        Assert.Equal(0, log.FOperatorId);
        Assert.Equal("system", log.FOperatorName);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 超时达2倍_autoReject动作_节点退回卡片退回()
    {
        const long stageDefId = 6502;
        const long cardId = 9602;
        const long stageInstanceId = 9702;
        using var db = TestDbContextFactory.Create(nameof(超时达2倍_autoReject动作_节点退回卡片退回));
        await SeedSingleAssigneeStageAsync(db, stageDefId, timeoutHours: 2,
            timeoutActionJson: """{"levels":[{"multiplier":2,"action":"autoReject"}]}""",
            cardId: cardId, stageInstanceId: stageInstanceId,
            activatedTime: DateTime.Now.AddHours(-5), assigneeFid: 9802);
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var job = CreateJob(db, engine, new FakeNotificationDispatcher());
        await job.ExecuteAsync();

        db.ChangeTracker.Clear();
        var stage = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == stageInstanceId);
        Assert.Equal("returned", stage.FStatus);
        Assert.Equal("rejected", stage.FFinalAction);
        Assert.Equal(2, stage.FTimeoutActionLevel);

        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == cardId);
        Assert.Equal("returned", card.FStatus);
        Assert.Null(card.FCurrentStageInstanceId);

        var assignee = await db.Set<CfStageAssignee>().AsNoTracking().SingleAsync(a => a.FStageInstanceId == stageInstanceId);
        Assert.Equal("rejected", assignee.FStatus);

        var log = await db.Set<CfActionLog>().AsNoTracking()
            .SingleAsync(l => l.FStageInstanceId == stageInstanceId && l.FActionType == "autoReject");
        Assert.Equal(0, log.FOperatorId);
        Assert.Equal("system", log.FOperatorName);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 超时达2倍_escalate动作_追加上级为新处理人_节点不推进()
    {
        const long stageDefId = 6503;
        const long cardId = 9603;
        const long stageInstanceId = 9703;
        using var db = TestDbContextFactory.Create(nameof(超时达2倍_escalate动作_追加上级为新处理人_节点不推进));
        await SeedSingleAssigneeStageAsync(db, stageDefId, timeoutHours: 2,
            timeoutActionJson: """{"levels":[{"multiplier":2,"action":"escalate"}]}""",
            cardId: cardId, stageInstanceId: stageInstanceId,
            activatedTime: DateTime.Now.AddHours(-5), assigneeFid: 9803);
        SeedSuperiorChain(db, superiorAssigneeFid: 9901);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var job = CreateJob(db, engine, new FakeNotificationDispatcher());
        await job.ExecuteAsync();

        db.ChangeTracker.Clear();
        var stage = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == stageInstanceId);
        Assert.Equal("active", stage.FStatus); // 升级不推进节点
        Assert.Equal(2, stage.FTimeoutActionLevel);

        var assignees = await db.Set<CfStageAssignee>().AsNoTracking()
            .Where(a => a.FStageInstanceId == stageInstanceId).ToListAsync();
        Assert.Equal(2, assignees.Count); // 原处理人 + 新增上级
        Assert.Contains(assignees, a => a.FUserId == SuperiorId && a.FStatus == "pending");
        Assert.Contains(assignees, a => a.FUserId == ApproverId && a.FStatus == "pending"); // 原处理人保留不变

        var log = await db.Set<CfActionLog>().AsNoTracking()
            .SingleAsync(l => l.FStageInstanceId == stageInstanceId && l.FActionType == "escalate");
        Assert.Contains("已升级 1 人", log.FOpinion);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 未配置超时升级链_仅一次性标记不触发引擎动作_向后兼容()
    {
        const long stageDefId = 6504;
        const long cardId = 9604;
        const long stageInstanceId = 9704;
        using var db = TestDbContextFactory.Create(nameof(未配置超时升级链_仅一次性标记不触发引擎动作_向后兼容));
        await SeedSingleAssigneeStageAsync(db, stageDefId, timeoutHours: 2,
            timeoutActionJson: null,
            cardId: cardId, stageInstanceId: stageInstanceId,
            activatedTime: DateTime.Now.AddHours(-5), assigneeFid: 9804);
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var job = CreateJob(db, engine, new FakeNotificationDispatcher());
        await job.ExecuteAsync();

        db.ChangeTracker.Clear();
        var stage = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == stageInstanceId);
        Assert.True(stage.FIsTimeout);
        Assert.Equal("active", stage.FStatus); // 引擎未被调用，节点未流转
        Assert.Null(stage.FTimeoutActionLevel);

        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == cardId);
        Assert.Equal("active", card.FStatus);

        var logs = await db.Set<CfActionLog>().AsNoTracking()
            .Where(l => l.FStageInstanceId == stageInstanceId).ToListAsync();
        var log = Assert.Single(logs);
        Assert.Equal("timeout", log.FActionType);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 超时升级链_同级别重复调度_不重复升级不重复记录_幂等()
    {
        const long stageDefId = 6505;
        const long cardId = 9605;
        const long stageInstanceId = 9705;
        using var db = TestDbContextFactory.Create(nameof(超时升级链_同级别重复调度_不重复升级不重复记录_幂等));
        await SeedSingleAssigneeStageAsync(db, stageDefId, timeoutHours: 2,
            timeoutActionJson: """{"levels":[{"multiplier":2,"action":"escalate"}]}""",
            cardId: cardId, stageInstanceId: stageInstanceId,
            activatedTime: DateTime.Now.AddHours(-5), assigneeFid: 9805);
        SeedSuperiorChain(db, superiorAssigneeFid: 9902);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var dispatcher = new FakeNotificationDispatcher();

        await CreateJob(db, engine, dispatcher).ExecuteAsync();
        db.ChangeTracker.Clear();
        // 同一节点/同一级别再次被调度（模拟同 tick 内重入或下一 tick 未变化的情形）——幂等高水位应挡下
        await CreateJob(db, engine, dispatcher).ExecuteAsync();

        db.ChangeTracker.Clear();
        var assignees = await db.Set<CfStageAssignee>().AsNoTracking()
            .Where(a => a.FStageInstanceId == stageInstanceId).ToListAsync();
        Assert.Equal(2, assignees.Count); // 未重复追加上级

        var logs = await db.Set<CfActionLog>().AsNoTracking()
            .Where(l => l.FStageInstanceId == stageInstanceId && l.FActionType == "escalate").ToListAsync();
        Assert.Single(logs); // 未重复记录 ActionLog

        var stage = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == stageInstanceId);
        Assert.Equal(2, stage.FTimeoutActionLevel);
    }

    // ── 骨架 ──────────────────────────────────────────────────────────

    /// <summary>单 human 节点、单固定处理人（fixedUsers），供本文件各用例复用。</summary>
    private static async global::System.Threading.Tasks.Task SeedSingleAssigneeStageAsync(
        STOTOPDbContext db, long stageDefId, int timeoutHours, string? timeoutActionJson,
        long cardId, long stageInstanceId, DateTime activatedTime, long assigneeFid)
    {
        db.Set<SysUser>().Add(new SysUser { FID = ApproverId, FName = "处理人" });
        if (!await db.Set<CfFlowDefinition>().AnyAsync(f => f.FID == FlowDefId))
        {
            db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
            {
                FID = FlowDefId, FFlowName = "超时升级链回归", FFlowCode = "timeout-escalation-regression", FOrgId = 1,
                FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
            });
            db.Set<CfFlowVersion>().Add(new CfFlowVersion
            {
                FID = FlowVersionId, FFlowDefinitionId = FlowDefId, FStatus = "published", FIsCurrentVersion = true
            });
        }
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = stageDefId, FFlowVersionId = FlowVersionId, FSortOrder = 1, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FTimeoutHours = timeoutHours, FTimeoutActionJson = timeoutActionJson,
            FAssigneeConfigJson = """{"users":[{"userId":71,"userName":"处理人"}]}"""
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = cardId, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "超时升级链用例", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = stageInstanceId, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = stageInstanceId, FCardId = cardId, FStageDefinitionId = stageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active",
            FActivatedTime = activatedTime, FStartTime = activatedTime
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = assigneeFid, FStageInstanceId = stageInstanceId, FUserId = ApproverId, FUserName = "处理人",
            FStatus = "pending", FAssignedTime = activatedTime
        });
        await db.SaveChangesAsync();
    }

    /// <summary>escalate 用例的上级解析链：处理人当前生效主任职 FDirectSuperiorId 直指上级用户。</summary>
    private static void SeedSuperiorChain(STOTOPDbContext db, long superiorAssigneeFid)
    {
        db.Set<SysUser>().Add(new SysUser { FID = SuperiorId, FName = "上级" });
        db.Set<SysUserOrganization>().Add(new SysUserOrganization
        {
            FID = superiorAssigneeFid, FUserId = ApproverId, FOrgId = 500, FDirectSuperiorId = SuperiorId,
            FIsPrimaryOrg = 1, FStatus = 1, F是否当前 = true
        });
    }

    private static CardFlowTimeoutJob CreateJob(
        STOTOPDbContext db, IFlowEngineService engine, INotificationDispatcher dispatcher)
    {
        return new CardFlowTimeoutJob(
            db,
            new FakeHubContext(),
            dispatcher,
            engine,
            new SingleTenantIterationFake(),
            NullLogger<CardFlowTimeoutJob>.Instance);
    }

    /// <summary>复用 ApprovalRatioTests 的真实引擎骨架：跨切面（编号/schema校验/预算/通知/批次）打桩，
    /// 核心流转（会签判定/顺序审批/条件路由/审批人解析/待办落库）走真实实现，确保升级动作经真实引擎事务验证。</summary>
    private static FlowEngineService CreateEngine(STOTOPDbContext db)
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var orchestration = new OrchestrationEngineService(db, NullLogger<OrchestrationEngineService>.Instance);

        return new FlowEngineService(
            db,
            new FakeNumberSequenceService(),
            new FakeCardSchemaService(),
            new ApprovalModeHandler(),
            new SequentialApprovalRuntime(),
            new ReturnToStageRuntime(),
            new StageConfigParser(),
            new StageFieldAccessService(),
            new StageActionPolicyService(),
            new ConditionRuleEvaluator(),
            new ApproverResolver(db),
            new FakeBudgetOccupationService(),
            new DbTodoService(db),
            new FakeNotificationDispatcher(),
            new AutoPluginFactory(provider),
            provider,
            provider.GetRequiredService<IServiceScopeFactory>(),
            orchestration,
            new FakeBatchNotifier(),
            new FakeBatchLifecycleService(),
            NullLogger<FlowEngineService>.Instance);
    }

    /// <summary>InMemory 测试无租户过滤器语义，直接以单租户调用一次 action。</summary>
    private sealed class SingleTenantIterationFake : ITenantIterationService
    {
        public global::System.Threading.Tasks.Task ForEachActiveTenantAsync(
            Func<long, global::System.Threading.Tasks.Task> action, string reason = "tenant-iteration")
            => action(1);
    }

    // ── IHubContext<CardFlowHub> 手写最小 Fake（无既有 Fake、项目未引入 mocking 框架） ──

    private sealed class FakeHubContext : IHubContext<CardFlowHub>
    {
        public IHubClients Clients { get; } = new FakeHubClients();
        public IGroupManager Groups { get; } = new FakeGroupManager();
    }

    private sealed class FakeHubClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new NoopClientProxy();
        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public IClientProxy Group(string groupName) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class NoopClientProxy : IClientProxy
    {
        public global::System.Threading.Tasks.Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            => global::System.Threading.Tasks.Task.CompletedTask;
    }

    private sealed class FakeGroupManager : IGroupManager
    {
        public global::System.Threading.Tasks.Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => global::System.Threading.Tasks.Task.CompletedTask;
        public global::System.Threading.Tasks.Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => global::System.Threading.Tasks.Task.CompletedTask;
    }
}
