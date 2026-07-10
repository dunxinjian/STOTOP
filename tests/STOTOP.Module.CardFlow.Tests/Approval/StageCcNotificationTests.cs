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
/// M8-B：验证引擎在人工节点 onEnter/onApprove/onReject 三个时机按
/// CfStageDefinition.FCcConfigJson 配置创建抄送(cc)待办，并按 timing 精确匹配
/// （不匹配则不触发；empty/null/malformed 由 CcNotifyConfig.Parse 兜底为 null）。
/// </summary>
public class StageCcNotificationTests
{
    private const long FlowDefId = 3400;
    private const long FlowVersionId = 3401;
    private const long StageDefId = 6201;
    private const long ApproverId = 51;
    private const long CcUserId = 52;
    private const long InitiatorId = 88;

    [Fact]
    public async global::System.Threading.Tasks.Task Submit_OnEnterCcConfigured_CreatesCcTodoForConfiguredUser()
    {
        using var db = CreateNoTrackingDb(nameof(Submit_OnEnterCcConfigured_CreatesCcTodoForConfiguredUser));
        await SeedFlowAsync(db, """{"users":[{"userId":52,"userName":"抄送人"}],"timing":"onEnter","channels":["system"]}""");

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9601, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "cc用例-onEnter", FStatus = "draft", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.SubmitAsync(9601, InitiatorId);
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var ccTodo = await db.Set<CfTodoItem>().AsNoTracking()
            .SingleOrDefaultAsync(t => t.FCardId == 9601 && t.FType == "cc" && t.FHandlerId == CcUserId);
        Assert.NotNull(ccTodo);
        Assert.Equal("pending", ccTodo!.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Approve_OnApproveCcConfigured_CreatesCcTodoForConfiguredUser()
    {
        using var db = CreateNoTrackingDb(nameof(Approve_OnApproveCcConfigured_CreatesCcTodoForConfiguredUser));
        await SeedFlowAsync(db, """{"users":[{"userId":52,"userName":"抄送人"}],"timing":"onApprove","channels":["system"]}""");

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9602, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "cc用例-onApprove", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9702, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9702, FCardId = 9602, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 9802, FStageInstanceId = 9702, FUserId = ApproverId, FUserName = "审批人",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.ApproveAsync(9602, ApproverId, new ApproveRequest { Opinion = "同意" });
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var ccTodo = await db.Set<CfTodoItem>().AsNoTracking()
            .SingleOrDefaultAsync(t => t.FCardId == 9602 && t.FType == "cc" && t.FHandlerId == CcUserId);
        Assert.NotNull(ccTodo);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Reject_OnRejectCcConfigured_CreatesCcTodoForConfiguredUser()
    {
        using var db = CreateNoTrackingDb(nameof(Reject_OnRejectCcConfigured_CreatesCcTodoForConfiguredUser));
        await SeedFlowAsync(db, """{"users":[{"userId":52,"userName":"抄送人"}],"timing":"onReject","channels":["system"]}""");

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9604, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "cc用例-onReject", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9704, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9704, FCardId = 9604, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 9804, FStageInstanceId = 9704, FUserId = ApproverId, FUserName = "审批人",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.RejectAsync(9604, ApproverId, new RejectRequest { Opinion = "驳回补材料" });
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var ccTodo = await db.Set<CfTodoItem>().AsNoTracking()
            .SingleOrDefaultAsync(t => t.FCardId == 9604 && t.FType == "cc" && t.FHandlerId == CcUserId);
        Assert.NotNull(ccTodo);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Approve_TimingMismatch_DoesNotCreateCcTodo()
    {
        // 节点配置 timing="onReject"，但走的是 approve 动作 —— 不应触发抄送
        using var db = CreateNoTrackingDb(nameof(Approve_TimingMismatch_DoesNotCreateCcTodo));
        await SeedFlowAsync(db, """{"users":[{"userId":52,"userName":"抄送人"}],"timing":"onReject","channels":["system"]}""");

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9603, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "cc用例-mismatch", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9703, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9703, FCardId = 9603, FStageDefinitionId = StageDefId, FStageName = "审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 9803, FStageInstanceId = 9703, FUserId = ApproverId, FUserName = "审批人",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var result = await engine.ApproveAsync(9603, ApproverId, new ApproveRequest { Opinion = "同意" });
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var ccTodoCount = await db.Set<CfTodoItem>().AsNoTracking()
            .CountAsync(t => t.FCardId == 9603 && t.FType == "cc" && t.FHandlerId == CcUserId);
        Assert.Equal(0, ccTodoCount);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Submit_OnEnterDingtalkChannel_DispatchesPushForCcTodo()
    {
        using var db = CreateNoTrackingDb(nameof(Submit_OnEnterDingtalkChannel_DispatchesPushForCcTodo));
        await SeedFlowAsync(db, """{"users":[{"userId":52,"userName":"抄送人"}],"timing":"onEnter","channels":["system","dingtalk"]}""");

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9605, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "cc用例-dingtalk", FStatus = "draft", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var fakeDispatcher = new FakeNotificationDispatcher();
        var engine = CreateEngine(db, fakeDispatcher);
        var result = await engine.SubmitAsync(9605, InitiatorId);
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var ccTodo = await db.Set<CfTodoItem>().AsNoTracking()
            .SingleOrDefaultAsync(t => t.FCardId == 9605 && t.FType == "cc" && t.FHandlerId == CcUserId);
        Assert.NotNull(ccTodo);
        Assert.Contains(ccTodo!.FID, fakeDispatcher.DispatchedTodoIds);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Submit_OnEnterSystemChannelOnly_DoesNotDispatchPush()
    {
        using var db = CreateNoTrackingDb(nameof(Submit_OnEnterSystemChannelOnly_DoesNotDispatchPush));
        await SeedFlowAsync(db, """{"users":[{"userId":52,"userName":"抄送人"}],"timing":"onEnter","channels":["system"]}""");

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9606, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "cc用例-system-only", FStatus = "draft", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var fakeDispatcher = new FakeNotificationDispatcher();
        var engine = CreateEngine(db, fakeDispatcher);
        var result = await engine.SubmitAsync(9606, InitiatorId);
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var ccTodo = await db.Set<CfTodoItem>().AsNoTracking()
            .SingleOrDefaultAsync(t => t.FCardId == 9606 && t.FType == "cc" && t.FHandlerId == CcUserId);
        Assert.NotNull(ccTodo);
        Assert.Empty(fakeDispatcher.DispatchedTodoIds);
    }

    /// <summary>复现生产全局跟踪行为的 InMemory 上下文（默认 TrackAll 会掩盖不落库 bug）。</summary>
    private static STOTOP.Infrastructure.Data.STOTOPDbContext CreateNoTrackingDb(string name)
    {
        var db = TestDbContextFactory.Create(name);
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        return db;
    }

    /// <summary>公共流程骨架：单 human 节点（fixedUsers=审批人），可选 FCcConfigJson。</summary>
    private static async global::System.Threading.Tasks.Task SeedFlowAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, string? ccConfigJson)
    {
        db.Set<SysUser>().Add(new SysUser { FID = ApproverId, FName = "审批人" });
        db.Set<SysUser>().Add(new SysUser { FID = CcUserId, FName = "抄送人" });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = FlowDefId, FFlowName = "抄送触发回归", FFlowCode = "cc-notify-regression", FOrgId = 1,
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
            FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"审批人"}]}""",
            FCcConfigJson = ccConfigJson
        });
        await db.SaveChangesAsync();
    }

    private static FlowEngineService CreateEngine(STOTOP.Infrastructure.Data.STOTOPDbContext db, FakeNotificationDispatcher? notificationDispatcher = null)
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
            notificationDispatcher ?? new FakeNotificationDispatcher(),
            new AutoPluginFactory(provider),
            provider,
            provider.GetRequiredService<IServiceScopeFactory>(),
            orchestration,
            new FakeBatchNotifier(),
            new FakeBatchLifecycleService(),
            NullLogger<FlowEngineService>.Instance);
    }
}
