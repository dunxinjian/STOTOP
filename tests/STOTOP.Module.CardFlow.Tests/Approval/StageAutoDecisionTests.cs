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
/// M2-2 节点自动裁决：信封 autoDecision 条件命中时，人工节点激活后不分派处理人，
/// 直接自动通过（节点完成并推进）或自动拒绝（卡片对齐 reject 链置 returned）。
/// choke point = AssignStageHandlersAsync 开头，legacy 顺序推进与规则路由两模式共用。
/// </summary>
public class StageAutoDecisionTests
{
    private const long FlowDefId = 3400;
    private const long FlowVersionId = 3401;
    private const long Stage1DefId = 6201;
    private const long Stage2DefId = 6202;
    private const long ApproverId = 61;
    private const long InitiatorId = 89;

    [Fact]
    public async global::System.Threading.Tasks.Task 自动通过_条件命中_节点自动完成并写日志()
    {
        using var db = TestDbContextFactory.Create(nameof(自动通过_条件命中_节点自动完成并写日志));
        await SeedFlowAsync(db, autoDecisionMode: "autoApprove");

        SeedActiveCardAtStage1(db, cardId: 9751, stageInstanceId: 9851, assigneeId: 9951,
            dataJson: """{"amount":500}""");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.ApproveAsync(9751, ApproverId, new ApproveRequest { Opinion = "同意" });
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        // 节点2实例被自动完成
        var stage2 = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9751 && s.FStageDefinitionId == Stage2DefId);
        Assert.Equal("completed", stage2.FStatus);
        Assert.Equal("approved", stage2.FFinalAction);
        Assert.Equal("满足条件自动通过", stage2.FOpinion);

        // 无处理人分派、无待办
        Assert.False(await db.Set<CfStageAssignee>().AsNoTracking().AnyAsync(a => a.FStageInstanceId == stage2.FID));
        Assert.False(await db.Set<CfTodoItem>().AsNoTracking().AnyAsync(t => t.FStageInstanceId == stage2.FID));

        // 操作日志 action=autoApprove
        var log = await db.Set<CfActionLog>().AsNoTracking()
            .SingleAsync(l => l.FStageInstanceId == stage2.FID && l.FActionType == "autoApprove");
        Assert.Equal("满足条件自动通过", log.FOpinion);

        // 节点2为最后节点 → 卡片完成
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9751);
        Assert.Equal("completed", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 自动拒绝_条件命中_卡片置退回并写日志()
    {
        using var db = TestDbContextFactory.Create(nameof(自动拒绝_条件命中_卡片置退回并写日志));
        await SeedFlowAsync(db, autoDecisionMode: "autoReject");

        SeedActiveCardAtStage1(db, cardId: 9752, stageInstanceId: 9852, assigneeId: 9952,
            dataJson: """{"amount":500}""");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.ApproveAsync(9752, ApproverId, new ApproveRequest { Opinion = "同意" });
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var stage2 = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9752 && s.FStageDefinitionId == Stage2DefId);
        Assert.Equal("returned", stage2.FStatus);
        Assert.Equal("rejected", stage2.FFinalAction);

        var log = await db.Set<CfActionLog>().AsNoTracking()
            .SingleAsync(l => l.FStageInstanceId == stage2.FID && l.FActionType == "autoReject");
        Assert.Equal("满足条件自动拒绝", log.FOpinion);

        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9752);
        Assert.Equal("returned", card.FStatus);
        Assert.Null(card.FCurrentStageInstanceId);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 条件未命中_正常分派处理人()
    {
        using var db = TestDbContextFactory.Create(nameof(条件未命中_正常分派处理人));
        await SeedFlowAsync(db, autoDecisionMode: "autoApprove");

        // amount=5000 不满足 lte 1000 条件 → 不自动裁决
        SeedActiveCardAtStage1(db, cardId: 9753, stageInstanceId: 9853, assigneeId: 9953,
            dataJson: """{"amount":5000}""");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.ApproveAsync(9753, ApproverId, new ApproveRequest { Opinion = "同意" });
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var stage2 = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9753 && s.FStageDefinitionId == Stage2DefId);
        Assert.Equal("active", stage2.FStatus);
        Assert.True(await db.Set<CfStageAssignee>().AsNoTracking().AnyAsync(a => a.FStageInstanceId == stage2.FID));
    }

    /// <summary>流程骨架：节点1（人工，固定处理人）→ 节点2（人工，带 autoDecision 信封，条件 amount&lt;=1000）。</summary>
    private static async global::System.Threading.Tasks.Task SeedFlowAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, string autoDecisionMode)
    {
        db.Set<SysUser>().Add(new SysUser { FID = ApproverId, FName = "审批人" });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = FlowDefId, FFlowName = "自动裁决回归", FFlowCode = "auto-decision-regression", FOrgId = 1,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = FlowVersionId, FFlowDefinitionId = FlowDefId, FStatus = "published", FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition
            {
                FID = Stage1DefId, FFlowVersionId = FlowVersionId, FSortOrder = 1, FStageName = "人工审批",
                FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
                FAssigneeConfigJson = """{"users":[{"userId":61,"userName":"审批人"}]}"""
            },
            new CfStageDefinition
            {
                FID = Stage2DefId, FFlowVersionId = FlowVersionId, FSortOrder = 2, FStageName = "自动裁决节点",
                FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
                FAssigneeConfigJson = """{"users":[{"userId":61,"userName":"审批人"}]}""",
                FInputFieldsJson =
                    """{"version":2,"inputFields":[],"actionPolicy":{"allowedActions":["approve","reject"]},"autoDecision":{"mode":"__MODE__","conditionJson":"{\"field\":\"amount\",\"operator\":\"lte\",\"value\":1000}"}}"""
                        .Replace("__MODE__", autoDecisionMode)
            });
        await db.SaveChangesAsync();
    }

    private static void SeedActiveCardAtStage1(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, long cardId, long stageInstanceId, long assigneeId, string dataJson)
    {
        db.Set<CfCard>().Add(new CfCard
        {
            FID = cardId, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "自动裁决用例", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = stageInstanceId, FCurrentRound = 1, FOrgId = 1, FDataJson = dataJson
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = stageInstanceId, FCardId = cardId, FStageDefinitionId = Stage1DefId, FStageName = "人工审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = assigneeId, FStageInstanceId = stageInstanceId, FUserId = ApproverId, FUserName = "审批人",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
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
