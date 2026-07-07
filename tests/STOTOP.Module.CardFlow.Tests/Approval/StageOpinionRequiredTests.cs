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
/// M2-6 意见必填校验：信封 actionPolicy.opinionRequiredActions 含当前动作且意见空白时，
/// 动作入口（事务前）抛 InvalidOperationException（全局异常中间件透传 400）。
/// </summary>
public class StageOpinionRequiredTests
{
    private const long FlowDefId = 3500;
    private const long FlowVersionId = 3501;
    private const long StageDefId = 6301;
    private const long ApproverId = 71;
    private const long InitiatorId = 90;

    [Fact]
    public async global::System.Threading.Tasks.Task 配置拒绝必填意见_空意见拒绝_抛异常()
    {
        using var db = TestDbContextFactory.Create(nameof(配置拒绝必填意见_空意见拒绝_抛异常));
        await SeedAsync(db, cardId: 9761, stageInstanceId: 9861, assigneeId: 9961);
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RejectAsync(9761, ApproverId, new RejectRequest { Opinion = "  " }));
        Assert.Equal("该操作需要填写处理意见", ex.Message);

        // 未进入事务：卡片/节点状态不变
        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9761);
        Assert.Equal("active", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 配置拒绝必填意见_带意见拒绝_正常退回()
    {
        using var db = TestDbContextFactory.Create(nameof(配置拒绝必填意见_带意见拒绝_正常退回));
        await SeedAsync(db, cardId: 9762, stageInstanceId: 9862, assigneeId: 9962);
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.RejectAsync(9762, ApproverId, new RejectRequest { Opinion = "材料不全" });
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9762);
        Assert.Equal("returned", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 未配置必填的动作_空意见通过_不受影响()
    {
        using var db = TestDbContextFactory.Create(nameof(未配置必填的动作_空意见通过_不受影响));
        await SeedAsync(db, cardId: 9763, stageInstanceId: 9863, assigneeId: 9963);
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        // 信封只配置了 reject 必填；approve 空意见应正常
        var result = await engine.ApproveAsync(9763, ApproverId, new ApproveRequest { Opinion = null });
        Assert.True(result.Success, result.Message);
    }

    private static async global::System.Threading.Tasks.Task SeedAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, long cardId, long stageInstanceId, long assigneeId)
    {
        db.Set<SysUser>().Add(new SysUser { FID = ApproverId, FName = "审批人" });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = FlowDefId, FFlowName = "意见必填回归", FFlowCode = "opinion-required-regression", FOrgId = 1,
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
            FAssigneeConfigJson = """{"users":[{"userId":71,"userName":"审批人"}]}""",
            FInputFieldsJson =
                """{"version":2,"inputFields":[],"actionPolicy":{"allowedActions":["approve","reject","transfer"],"opinionRequiredActions":["reject"]}}"""
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = cardId, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "意见必填用例", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
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
