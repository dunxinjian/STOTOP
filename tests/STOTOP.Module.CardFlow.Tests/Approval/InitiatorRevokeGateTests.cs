using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

/// <summary>
/// M8-D 件②：允许发起人撤回(定义级) gate。WithdrawAsync 读卡片锁定版本 FFlowSettingsJson 的
/// allowInitiatorRevoke：缺失/true → 放行（保留现状）；仅显式 false → 拦。发起人/active/无人已审等既有校验不变。
/// </summary>
public class InitiatorRevokeGateTests
{
    private const long FlowDefId = 3700;
    private const long FlowVersionId = 3701;
    private const long StageDefId = 6701;
    private const long ApproverId = 51;
    private const long InitiatorId = 88;
    private const long OtherUserId = 77;   // 非发起人、非处理人

    [Fact]
    public async global::System.Threading.Tasks.Task Withdraw_AllowInitiatorRevokeFalse_IsRejected()
    {
        using var db = CreateNoTrackingDb(nameof(Withdraw_AllowInitiatorRevokeFalse_IsRejected));
        await SeedActiveCardAsync(db, cardId: 9710, stageInstanceId: 9810, assigneeId: 9910,
            flowSettingsJson: """{"allowInitiatorRevoke":false}""");

        var engine = CreateEngine(db);
        var result = await engine.WithdrawAsync(9710, InitiatorId);

        Assert.False(result.Success);
        Assert.Equal("该流程不允许发起人撤回", result.Message);
        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9710);
        Assert.Equal("active", card.FStatus);   // 未被撤回
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Withdraw_AllowInitiatorRevokeMissing_Succeeds()
    {
        using var db = CreateNoTrackingDb(nameof(Withdraw_AllowInitiatorRevokeMissing_Succeeds));
        await SeedActiveCardAsync(db, cardId: 9711, stageInstanceId: 9811, assigneeId: 9911,
            flowSettingsJson: null);   // 存量流程：无该键 → 缺失即允许

        var engine = CreateEngine(db);
        var result = await engine.WithdrawAsync(9711, InitiatorId);

        Assert.True(result.Success, result.Message);
        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9711);
        Assert.Equal("draft", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Withdraw_AllowInitiatorRevokeTrue_Succeeds()
    {
        using var db = CreateNoTrackingDb(nameof(Withdraw_AllowInitiatorRevokeTrue_Succeeds));
        await SeedActiveCardAsync(db, cardId: 9712, stageInstanceId: 9812, assigneeId: 9912,
            flowSettingsJson: """{"allowInitiatorRevoke":true}""");

        var engine = CreateEngine(db);
        var result = await engine.WithdrawAsync(9712, InitiatorId);

        Assert.True(result.Success, result.Message);
        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9712);
        Assert.Equal("draft", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Withdraw_NonInitiator_IsRejected_RegardlessOfGate()
    {
        using var db = CreateNoTrackingDb(nameof(Withdraw_NonInitiator_IsRejected_RegardlessOfGate));
        await SeedActiveCardAsync(db, cardId: 9713, stageInstanceId: 9813, assigneeId: 9913,
            flowSettingsJson: null);   // 即使允许撤回，非发起人/代提交人仍被既有校验拦（gate 在其后，不越位）

        var engine = CreateEngine(db);
        var result = await engine.WithdrawAsync(9713, OtherUserId);

        Assert.False(result.Success);
        Assert.Equal("只有发起人或代提交人可以撤回", result.Message);
    }

    /// <summary>播种一张 active 卡：当前节点 human/active + 唯一 pending 处理人；版本携带指定 flowSettingsJson。</summary>
    private static async global::System.Threading.Tasks.Task SeedActiveCardAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db,
        long cardId, long stageInstanceId, long assigneeId, string? flowSettingsJson)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = FlowDefId, FFlowName = "撤回gate回归", FFlowCode = $"revoke-gate-{cardId}", FOrgId = 1,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = FlowVersionId, FFlowDefinitionId = FlowDefId, FStatus = "published", FIsCurrentVersion = true,
            FFlowSettingsJson = flowSettingsJson
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = StageDefId, FFlowVersionId = FlowVersionId, FSortOrder = 1, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"审批人"}]}"""
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = cardId, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "撤回gate", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = stageInstanceId, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = stageInstanceId, FCardId = cardId, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = assigneeId, FStageInstanceId = stageInstanceId, FUserId = ApproverId, FUserName = "审批人",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static STOTOP.Infrastructure.Data.STOTOPDbContext CreateNoTrackingDb(string name)
    {
        var db = TestDbContextFactory.Create(name);
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        return db;
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
