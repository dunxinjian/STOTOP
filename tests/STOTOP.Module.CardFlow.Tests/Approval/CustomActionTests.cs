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
/// M8-C（件③ Task 7+8）：验证引擎 ExecuteCustomActionAsync 按节点 ActionPolicy.CustomActions
/// （存于 FInputFieldsJson version=2 信封，无新列）分派处理器：
/// autoApprove → 复用 ApproveAsync（卡片推进/完成）；autoReject → 复用 RejectAsync（卡片退回）；
/// notify → 复用 M8-B 抄送机制（timing="onCustomAction"）+ 落动作日志。
/// 非当前处理人 / 动作编码不存在 / 必填意见缺失 → Fail，不落库。
/// </summary>
public class CustomActionTests
{
    private const long FlowDefId = 3500;
    private const long FlowVersionId = 3501;
    private const long StageDefId = 6301;
    private const long ApproverId = 71;
    private const long OtherUserId = 72;
    private const long CcUserId = 73;
    private const long InitiatorId = 88;

    private const string CustomActionsJson = """
        {
          "version": 2,
          "inputFields": [],
          "actionPolicy": {
            "allowedActions": ["approve", "reject"],
            "customActions": [
              { "code": "quickApprove", "label": "快速通过", "handler": "autoApprove", "requireOpinion": false },
              { "code": "quickReject", "label": "快速驳回", "handler": "autoReject", "requireOpinion": true },
              { "code": "pingCc", "label": "提醒抄送", "handler": "notify", "requireOpinion": false }
            ]
          }
        }
        """;

    [Fact]
    public async global::System.Threading.Tasks.Task ExecuteCustomAction_AutoApproveHandler_CardAdvancesToCompleted()
    {
        using var db = CreateNoTrackingDb(nameof(ExecuteCustomAction_AutoApproveHandler_CardAdvancesToCompleted));
        await SeedFlowAsync(db, CustomActionsJson, null);
        await SeedActiveCardAsync(db, cardId: 9701, stageInstanceId: 9801, assigneeId: 9901);

        var engine = CreateEngine(db);
        var result = await engine.ExecuteCustomActionAsync(9701, ApproverId, "quickApprove", null);
        Assert.True(result.Success, result.Message);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9701);
        Assert.Equal("completed", card.FStatus);

        var assignee = await db.Set<CfStageAssignee>().AsNoTracking().SingleAsync(a => a.FID == 9901);
        Assert.Equal("approved", assignee.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ExecuteCustomAction_AutoRejectHandler_CardReturned()
    {
        using var db = CreateNoTrackingDb(nameof(ExecuteCustomAction_AutoRejectHandler_CardReturned));
        await SeedFlowAsync(db, CustomActionsJson, null);
        await SeedActiveCardAsync(db, cardId: 9702, stageInstanceId: 9802, assigneeId: 9902);

        var engine = CreateEngine(db);
        var result = await engine.ExecuteCustomActionAsync(9702, ApproverId, "quickReject", "材料不全");
        Assert.True(result.Success, result.Message);
        Assert.Equal("returned", result.NewStatus);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9702);
        Assert.Equal("returned", card.FStatus);

        var assignee = await db.Set<CfStageAssignee>().AsNoTracking().SingleAsync(a => a.FID == 9902);
        Assert.Equal("rejected", assignee.FStatus);
        Assert.Equal("材料不全", assignee.FOpinion);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ExecuteCustomAction_UnknownActionCode_Fails()
    {
        using var db = CreateNoTrackingDb(nameof(ExecuteCustomAction_UnknownActionCode_Fails));
        await SeedFlowAsync(db, CustomActionsJson, null);
        await SeedActiveCardAsync(db, cardId: 9703, stageInstanceId: 9803, assigneeId: 9903);

        var engine = CreateEngine(db);
        var result = await engine.ExecuteCustomActionAsync(9703, ApproverId, "doesNotExist", null);

        Assert.False(result.Success);
        Assert.Equal("自定义动作不存在", result.Message);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9703);
        Assert.Equal("active", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ExecuteCustomAction_NonAssignee_Fails()
    {
        using var db = CreateNoTrackingDb(nameof(ExecuteCustomAction_NonAssignee_Fails));
        await SeedFlowAsync(db, CustomActionsJson, null);
        await SeedActiveCardAsync(db, cardId: 9704, stageInstanceId: 9804, assigneeId: 9904);

        var engine = CreateEngine(db);
        // OtherUserId 不是该节点的待处理人
        var result = await engine.ExecuteCustomActionAsync(9704, OtherUserId, "quickApprove", null);

        Assert.False(result.Success);
        Assert.Equal("您不是当前节点处理人", result.Message);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9704);
        Assert.Equal("active", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ExecuteCustomAction_RequireOpinionMissing_Fails()
    {
        using var db = CreateNoTrackingDb(nameof(ExecuteCustomAction_RequireOpinionMissing_Fails));
        await SeedFlowAsync(db, CustomActionsJson, null);
        await SeedActiveCardAsync(db, cardId: 9705, stageInstanceId: 9805, assigneeId: 9905);

        var engine = CreateEngine(db);
        // quickReject 配置 requireOpinion=true，未传意见应直接 Fail，不触发 RejectAsync
        var result = await engine.ExecuteCustomActionAsync(9705, ApproverId, "quickReject", null);

        Assert.False(result.Success);
        Assert.Equal("该动作需要填写处理意见", result.Message);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9705);
        Assert.Equal("active", card.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ExecuteCustomAction_NotifyHandler_FiresCcAndLogsAction()
    {
        using var db = CreateNoTrackingDb(nameof(ExecuteCustomAction_NotifyHandler_FiresCcAndLogsAction));
        await SeedFlowAsync(db, CustomActionsJson,
            """{"users":[{"userId":73,"userName":"抄送人"}],"timing":"onCustomAction","channels":["system"]}""");
        await SeedActiveCardAsync(db, cardId: 9706, stageInstanceId: 9806, assigneeId: 9906);

        var engine = CreateEngine(db);
        var result = await engine.ExecuteCustomActionAsync(9706, ApproverId, "pingCc", null);
        Assert.True(result.Success, result.Message);
        Assert.Equal("已触发通知", result.Message);

        db.ChangeTracker.Clear();
        // 卡片/节点/处理人状态不受 notify 影响（非 approve/reject）
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9706);
        Assert.Equal("active", card.FStatus);

        var ccTodo = await db.Set<CfTodoItem>().AsNoTracking()
            .SingleOrDefaultAsync(t => t.FCardId == 9706 && t.FType == "cc" && t.FHandlerId == CcUserId);
        Assert.NotNull(ccTodo);

        var actionLog = await db.Set<CfActionLog>().AsNoTracking()
            .SingleOrDefaultAsync(l => l.FCardId == 9706 && l.FActionType == "customAction:notify");
        Assert.NotNull(actionLog);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ExecuteCustomAction_NotifyHandler_NoCcConfig_HonestMessageNoFalseSuccess()
    {
        // review 修复回归（M8-C 件③）：节点未配置 onCustomAction 抄送时，notify 不得谎称"已触发通知"，
        // 须落诚实消息且仍要记录动作日志、不产生 cc 待办。
        using var db = CreateNoTrackingDb(nameof(ExecuteCustomAction_NotifyHandler_NoCcConfig_HonestMessageNoFalseSuccess));
        await SeedFlowAsync(db, CustomActionsJson, ccConfigJson: null);
        await SeedActiveCardAsync(db, cardId: 9708, stageInstanceId: 9808, assigneeId: 9908);

        var engine = CreateEngine(db);
        var result = await engine.ExecuteCustomActionAsync(9708, ApproverId, "pingCc", null);
        Assert.True(result.Success, result.Message);
        Assert.Equal("已记录动作（该节点未配置自定义动作触发的抄送，无通知发送）", result.Message);

        db.ChangeTracker.Clear();
        var ccTodo = await db.Set<CfTodoItem>().AsNoTracking()
            .SingleOrDefaultAsync(t => t.FCardId == 9708 && t.FType == "cc");
        Assert.Null(ccTodo);

        var actionLog = await db.Set<CfActionLog>().AsNoTracking()
            .SingleOrDefaultAsync(l => l.FCardId == 9708 && l.FActionType == "customAction:notify");
        Assert.NotNull(actionLog);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ExecuteCustomAction_WebhookHandler_RejectedByEngine_NoExternalCall()
    {
        using var db = CreateNoTrackingDb(nameof(ExecuteCustomAction_WebhookHandler_RejectedByEngine_NoExternalCall));
        const string webhookActionJson = """
            {
              "version": 2,
              "inputFields": [],
              "actionPolicy": {
                "allowedActions": ["approve", "reject"],
                "customActions": [
                  { "code": "wh", "label": "外部", "handler": "webhook", "requireOpinion": false }
                ]
              }
            }
            """;
        await SeedFlowAsync(db, webhookActionJson, null);
        await SeedActiveCardAsync(db, cardId: 9707, stageInstanceId: 9807, assigneeId: 9907);

        var engine = CreateEngine(db);
        // handler 为未知的 "webhook"（引擎 switch 无匹配 case）：SSRF 延期护栏——须被 default→Fail 拒绝，不得触发外部调用
        var result = await engine.ExecuteCustomActionAsync(9707, ApproverId, "wh", null);

        Assert.False(result.Success);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9707);
        Assert.Equal("active", card.FStatus);

        var stageInstance = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == 9807);
        Assert.Equal("active", stageInstance.FStatus);

        var assignee = await db.Set<CfStageAssignee>().AsNoTracking().SingleAsync(a => a.FID == 9907);
        Assert.Equal("pending", assignee.FStatus);
    }

    /// <summary>复现生产全局跟踪行为的 InMemory 上下文（默认 TrackAll 会掩盖不落库 bug）。</summary>
    private static STOTOP.Infrastructure.Data.STOTOPDbContext CreateNoTrackingDb(string name)
    {
        var db = TestDbContextFactory.Create(name);
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        return db;
    }

    /// <summary>公共流程骨架：单 human 节点（fixedUsers=审批人），FInputFieldsJson 携带自定义动作信封，可选 FCcConfigJson。</summary>
    private static async global::System.Threading.Tasks.Task SeedFlowAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, string inputFieldsJson, string? ccConfigJson)
    {
        db.Set<SysUser>().Add(new SysUser { FID = ApproverId, FName = "审批人" });
        db.Set<SysUser>().Add(new SysUser { FID = OtherUserId, FName = "无关用户" });
        db.Set<SysUser>().Add(new SysUser { FID = CcUserId, FName = "抄送人" });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = FlowDefId, FFlowName = "自定义动作回归", FFlowCode = "custom-action-regression", FOrgId = 1,
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
            FInputFieldsJson = inputFieldsJson,
            FCcConfigJson = ccConfigJson
        });
        await db.SaveChangesAsync();
    }

    /// <summary>每个用例独立的 active 卡片 + 活跃节点实例 + 待处理人（互不共享主键，避免用例间串数据）。</summary>
    private static async global::System.Threading.Tasks.Task SeedActiveCardAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, long cardId, long stageInstanceId, long assigneeId)
    {
        db.Set<CfCard>().Add(new CfCard
        {
            FID = cardId, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "自定义动作用例", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
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
