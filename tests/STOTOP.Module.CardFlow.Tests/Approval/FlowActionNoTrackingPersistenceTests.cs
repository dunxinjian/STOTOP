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
/// 回归：生产环境全局 NoTrackingWithIdentityResolution（Program.cs）下，
/// FlowEngineService 的 Reject/Withdraw/Resubmit/Void/Resume 查询到的实体均未跟踪，
/// 改属性后若不显式 _dbContext.Entry(x).State = Modified，SaveChangesAsync 不产生 UPDATE，
/// 流转动作"看似成功实则不落库"。测试 DbContext 默认 TrackAll 会掩盖该问题，
/// 故每个用例显式切到 NoTrackingWithIdentityResolution 复现生产行为，
/// 断言前 ChangeTracker.Clear() 后重查数据库验证真实落库结果。
/// </summary>
public class FlowActionNoTrackingPersistenceTests
{
    private const long FlowDefId = 3300;
    private const long FlowVersionId = 3301;
    private const long StageDefId = 6101;
    private const long ApproverId = 51;
    private const long InitiatorId = 88;

    [Fact]
    public async global::System.Threading.Tasks.Task Reject_UnderNoTracking_PersistsReturnedStatus()
    {
        using var db = CreateNoTrackingDb(nameof(Reject_UnderNoTracking_PersistsReturnedStatus));
        await SeedFlowAsync(db);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9701, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "退回用例", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9801, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9801, FCardId = 9701, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 9901, FStageInstanceId = 9801, FUserId = ApproverId, FUserName = "审批人",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.RejectAsync(9701, ApproverId, new RejectRequest { Opinion = "退回补材料" });
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9701);
        Assert.Equal("returned", card.FStatus);
        Assert.Null(card.FCurrentStageInstanceId);
        var stage = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == 9801);
        Assert.Equal("returned", stage.FStatus);
        var assignee = await db.Set<CfStageAssignee>().AsNoTracking().SingleAsync(a => a.FID == 9901);
        Assert.Equal("rejected", assignee.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Withdraw_UnderNoTracking_PersistsDraftStatus()
    {
        using var db = CreateNoTrackingDb(nameof(Withdraw_UnderNoTracking_PersistsDraftStatus));
        await SeedFlowAsync(db);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9702, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "撤回用例", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9802, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9802, FCardId = 9702, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 9902, FStageInstanceId = 9802, FUserId = ApproverId, FUserName = "审批人",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.WithdrawAsync(9702, InitiatorId);
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9702);
        Assert.Equal("draft", card.FStatus);
        Assert.Null(card.FCurrentStageInstanceId);
        var stage = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == 9802);
        Assert.Equal("cancelled", stage.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Resubmit_UnderNoTracking_PersistsActiveStatusAndRoundIncrement()
    {
        using var db = CreateNoTrackingDb(nameof(Resubmit_UnderNoTracking_PersistsActiveStatusAndRoundIncrement));
        await SeedFlowAsync(db);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9703, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "重提用例", FStatus = "returned", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.ResubmitAsync(9703, InitiatorId);
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9703);
        Assert.Equal("active", card.FStatus);
        Assert.Equal(2, card.FCurrentRound);
        // FCurrentStageInstanceId 在中途 SaveChanges 之后才赋值，验证变更检测同样落库
        var newStage = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9703 && s.FRound == 2);
        Assert.Equal("active", newStage.FStatus);
        Assert.Equal(newStage.FID, card.FCurrentStageInstanceId);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Void_UnderNoTracking_PersistsVoidedStatus()
    {
        using var db = CreateNoTrackingDb(nameof(Void_UnderNoTracking_PersistsVoidedStatus));
        await SeedFlowAsync(db);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9704, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "作废用例", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9804, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9804, FCardId = 9704, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.VoidAsync(9704, InitiatorId, "不再需要");
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9704);
        Assert.Equal("voided", card.FStatus);
        Assert.Null(card.FCurrentStageInstanceId);
        var stage = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == 9804);
        Assert.Equal("cancelled", stage.FStatus);
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
            FID = FlowDefId, FFlowName = "流转落库回归", FFlowCode = "no-tracking-regression", FOrgId = 1,
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
