using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

/// <summary>
/// 代提交（FAgentId）场景下 FlowEngineService 的操作门禁放宽回归：
/// 代理人（FAgentId）应与发起人（FInitiatorId）一样可提交/重提被代理人的卡片，
/// 无关人员仍应被拒绝。mirror <see cref="FlowActionNoTrackingPersistenceTests"/> 的 helper 与 NoTracking 复现手法。
/// </summary>
public class OnBehalfSubmitGateTests
{
    private const long FlowDefId = 3310;
    private const long FlowVersionId = 3311;
    private const long StageDefId = 6111;
    private const long ApproverId = 51;

    [Fact]
    public async global::System.Threading.Tasks.Task 代理人可提交被代理人的卡片()
    {
        using var db = CreateNoTrackingDb(nameof(代理人可提交被代理人的卡片));
        await SeedFlowAsync(db);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9720, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "代提交", FStatus = "draft", FInitiatorId = 901, FInitiatorName = "被代理人",
            FAgentId = 900, FAgentName = "代理人",
            FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await CreateEngine(db).SubmitAsync(9720, 900); // 代理人900 提交
        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 无关人员不能提交()
    {
        using var db = CreateNoTrackingDb(nameof(无关人员不能提交));
        await SeedFlowAsync(db);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9721, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "代提交", FStatus = "draft", FInitiatorId = 901, FInitiatorName = "被代理人",
            FAgentId = null,
            FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await CreateEngine(db).SubmitAsync(9721, 902); // 无关人员902
        Assert.False(result.Success);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 代理人可重提被代理人退回的卡片()
    {
        using var db = CreateNoTrackingDb(nameof(代理人可重提被代理人退回的卡片));
        await SeedFlowAsync(db);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9722, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "代提交-重提", FStatus = "returned", FInitiatorId = 901, FInitiatorName = "被代理人",
            FAgentId = 900, FAgentName = "代理人",
            FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        // 上一轮被驳回的节点实例，供 resubmitStrategy=fromRejected 场景下追溯（缺省 fromStart 亦不受影响）
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9822, FCardId = 9722, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "returned",
            FFinalAction = "rejected", FCompletedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await CreateEngine(db).ResubmitAsync(9722, 900); // 代理人900 重提
        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 代理人可撤回被代理人提交的卡片()
    {
        using var db = CreateNoTrackingDb(nameof(代理人可撤回被代理人提交的卡片));
        await SeedFlowAsync(db);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9723, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "代提交-撤回", FStatus = "active", FInitiatorId = 901, FInitiatorName = "被代理人",
            FAgentId = 900, FAgentName = "代理人",
            FCurrentStageInstanceId = 9823, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9823, FCardId = 9723, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await CreateEngine(db).WithdrawAsync(9723, 900); // 代理人900 撤回
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9723);
        Assert.Equal("draft", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 无关人员不能撤回()
    {
        using var db = CreateNoTrackingDb(nameof(无关人员不能撤回));
        await SeedFlowAsync(db);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9724, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "代提交-撤回", FStatus = "active", FInitiatorId = 901, FInitiatorName = "被代理人",
            FAgentId = null,
            FCurrentStageInstanceId = 9824, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9824, FCardId = 9724, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await CreateEngine(db).WithdrawAsync(9724, 902); // 无关人员902
        Assert.False(result.Success);
    }

    /// <summary>复现生产全局跟踪行为的 InMemory 上下文（默认 TrackAll 会掩盖不落库 bug）。</summary>
    private static STOTOP.Infrastructure.Data.STOTOPDbContext CreateNoTrackingDb(string name)
    {
        var db = TestDbContextFactory.Create(name);
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        return db;
    }

    /// <summary>公共流程骨架：单 human 节点（fixedUsers=审批人），FID 独立于其他测试类。</summary>
    private static async global::System.Threading.Tasks.Task SeedFlowAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<SysUser>().Add(new SysUser { FID = ApproverId, FName = "审批人" });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = FlowDefId, FFlowName = "代提交门禁回归", FFlowCode = "on-behalf-submit-gate", FOrgId = 1,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = FlowVersionId, FFlowDefinitionId = FlowDefId, FStatus = "published", FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = StageDefId, FFlowVersionId = FlowVersionId, FSortOrder = 1, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"审批人"}]}"""
        });
        await db.SaveChangesAsync();
    }

    private static FlowEngineService CreateEngine(STOTOP.Infrastructure.Data.STOTOPDbContext db)
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
}
