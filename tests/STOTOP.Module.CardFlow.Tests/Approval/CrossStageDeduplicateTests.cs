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
/// M8-C 件②：跨节点审批人去重(FSkipDuplicateApprover)。
/// 场景：A→B 两个人工节点，用户 51 同时是 A、B 的固定处理人。B 打开去重开关后，
/// A 审批过的用户 51 不应再出现在 B 的处理人列表中；若去重后 B 无人可分派，
/// 视为节点自动通过直接推进（镜像 TryApplyAutoDecisionAsync 的自动通过路径）。
/// 默认 FSkipDuplicateApprover=false 不去重，验证向后兼容。
/// </summary>
public class CrossStageDeduplicateTests
{
    private const long ApproverA = 51; // 与 A、B 两节点重复的处理人
    private const long ApproverOnlyAtB = 52; // 只出现在 B 节点的处理人
    private const long InitiatorId = 88;

    // ── 场景1：B 唯一处理人与 A 重复 → 去重后 B 无人可分派 → 自动通过并整卡完成 ──

    [Fact]
    public async global::System.Threading.Tasks.Task Approve_StageA_SkipDuplicateApprover_NoApproversLeft_AutoCompletesStageAndCard()
    {
        const long flowDefId = 3600;
        const long flowVersionId = 3601;
        const long stageDefIdA = 6601;
        const long stageDefIdB = 6602;

        using var db = CreateNoTrackingDb(nameof(Approve_StageA_SkipDuplicateApprover_NoApproversLeft_AutoCompletesStageAndCard));
        await SeedTwoStageFlowAsync(
            db, flowDefId, flowVersionId, stageDefIdA, stageDefIdB,
            stageBUsersJson: """{"users":[{"userId":51,"userName":"审批人A"}]}""",
            stageBSkipDuplicateApprover: true);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9801, FFlowDefinitionId = flowDefId, FFlowVersionId = flowVersionId,
            FTitle = "跨节点去重-全部剔除", FStatus = "draft", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var submitResult = await engine.SubmitAsync(9801, InitiatorId);
        Assert.True(submitResult.Success, submitResult.Message);

        db.ChangeTracker.Clear();
        var stageAInstance = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9801 && s.FStageDefinitionId == stageDefIdA);

        var approveResult = await engine.ApproveAsync(9801, ApproverA, new ApproveRequest { Opinion = "同意" });
        Assert.True(approveResult.Success, approveResult.Message);

        db.ChangeTracker.Clear();

        // B 节点实例应已创建，但去重后 51 是唯一处理人 → 无人可分派 → 自动通过
        var stageBInstance = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9801 && s.FStageDefinitionId == stageDefIdB);
        Assert.Equal("completed", stageBInstance.FStatus);
        Assert.Equal("approved", stageBInstance.FFinalAction);

        var stageBAssigneeCount = await db.Set<CfStageAssignee>().AsNoTracking()
            .CountAsync(a => a.FStageInstanceId == stageBInstance.FID);
        Assert.Equal(0, stageBAssigneeCount); // 去重剔除后没有生成任何处理人记录

        // B 是最后一个节点，自动通过后应整卡完成
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9801);
        Assert.Equal("completed", card.FStatus);
    }

    // ── 场景2：B 有两个处理人（51 重复 + 52 不重复）→ 去重后仅剔除 51，保留 52 ──

    [Fact]
    public async global::System.Threading.Tasks.Task Approve_StageA_SkipDuplicateApprover_PartialOverlap_RemovesOnlyDuplicateUser()
    {
        const long flowDefId = 3610;
        const long flowVersionId = 3611;
        const long stageDefIdA = 6611;
        const long stageDefIdB = 6612;

        using var db = CreateNoTrackingDb(nameof(Approve_StageA_SkipDuplicateApprover_PartialOverlap_RemovesOnlyDuplicateUser));
        await SeedTwoStageFlowAsync(
            db, flowDefId, flowVersionId, stageDefIdA, stageDefIdB,
            stageBUsersJson: """{"users":[{"userId":51,"userName":"审批人A"},{"userId":52,"userName":"审批人B"}]}""",
            stageBSkipDuplicateApprover: true);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9802, FFlowDefinitionId = flowDefId, FFlowVersionId = flowVersionId,
            FTitle = "跨节点去重-部分重复", FStatus = "draft", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var submitResult = await engine.SubmitAsync(9802, InitiatorId);
        Assert.True(submitResult.Success, submitResult.Message);

        db.ChangeTracker.Clear();
        var approveResult = await engine.ApproveAsync(9802, ApproverA, new ApproveRequest { Opinion = "同意" });
        Assert.True(approveResult.Success, approveResult.Message);

        db.ChangeTracker.Clear();
        var stageBInstance = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9802 && s.FStageDefinitionId == stageDefIdB);
        Assert.Equal("active", stageBInstance.FStatus); // 仍有处理人(52)，未自动通过

        var stageBAssignees = await db.Set<CfStageAssignee>().AsNoTracking()
            .Where(a => a.FStageInstanceId == stageBInstance.FID)
            .ToListAsync();
        Assert.Single(stageBAssignees);
        Assert.Equal(ApproverOnlyAtB, stageBAssignees[0].FUserId); // 51 被剔除，只剩 52
    }

    // ── 场景3：FSkipDuplicateApprover=false（默认）→ 不去重，51 仍是 B 的处理人（向后兼容） ──

    [Fact]
    public async global::System.Threading.Tasks.Task Approve_StageA_SkipDuplicateApproverFalse_KeepsDuplicateUserAtStageB()
    {
        const long flowDefId = 3620;
        const long flowVersionId = 3621;
        const long stageDefIdA = 6621;
        const long stageDefIdB = 6622;

        using var db = CreateNoTrackingDb(nameof(Approve_StageA_SkipDuplicateApproverFalse_KeepsDuplicateUserAtStageB));
        await SeedTwoStageFlowAsync(
            db, flowDefId, flowVersionId, stageDefIdA, stageDefIdB,
            stageBUsersJson: """{"users":[{"userId":51,"userName":"审批人A"}]}""",
            stageBSkipDuplicateApprover: false);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9803, FFlowDefinitionId = flowDefId, FFlowVersionId = flowVersionId,
            FTitle = "不去重-向后兼容", FStatus = "draft", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var submitResult = await engine.SubmitAsync(9803, InitiatorId);
        Assert.True(submitResult.Success, submitResult.Message);

        db.ChangeTracker.Clear();
        var approveResult = await engine.ApproveAsync(9803, ApproverA, new ApproveRequest { Opinion = "同意" });
        Assert.True(approveResult.Success, approveResult.Message);

        db.ChangeTracker.Clear();
        var stageBInstance = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9803 && s.FStageDefinitionId == stageDefIdB);
        Assert.Equal("active", stageBInstance.FStatus); // 未去重，正常等待处理人审批

        var stageBAssignee = await db.Set<CfStageAssignee>().AsNoTracking()
            .SingleAsync(a => a.FStageInstanceId == stageBInstance.FID);
        Assert.Equal(ApproverA, stageBAssignee.FUserId); // 51 仍是 B 的处理人（不去重）
        Assert.Equal("pending", stageBAssignee.FStatus);
    }

    // ── 场景4：重提fromRejected时，B 不应因"本节点自身历史"（上一轮被驳回）而被误判为跨节点重复审批人并自动跳过 ──
    // 回归背景：dedup 查询若仅按 FCardId 过滤、不排除本节点自身历史，会把 B 上一轮"51 rejected"这条记录也计入
    // actedUserIds，导致重提后 B 的唯一处理人 51 被去重成空 → 自动通过 → 本该被打回重审的节点无人复核就静默放行。

    [Fact]
    public async global::System.Threading.Tasks.Task Resubmit_FromRejected_RejectedStageBNotSkippedByOwnPriorHistory()
    {
        const long flowDefId = 3630;
        const long flowVersionId = 3631;
        const long stageDefIdA = 6631;
        const long stageDefIdB = 6632;
        const long approverAtStageA = 100; // 只出现在 A，与 B 的处理人不重复
        const long approverAtStageB = 51; // B 的唯一固定处理人，B 开启去重

        using var db = CreateNoTrackingDb(nameof(Resubmit_FromRejected_RejectedStageBNotSkippedByOwnPriorHistory));
        db.Set<SysUser>().Add(new SysUser { FID = approverAtStageA, FName = "审批人A" });
        db.Set<SysUser>().Add(new SysUser { FID = approverAtStageB, FName = "审批人B" });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = flowDefId, FFlowName = "重提fromRejected自身历史去重回归", FFlowCode = $"cross-stage-dedup-{flowDefId}", FOrgId = 1,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = flowVersionId, FFlowDefinitionId = flowDefId, FStatus = "published", FIsCurrentVersion = true,
            FFlowSettingsJson = """{"resubmitStrategy":"fromRejected"}"""
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = stageDefIdA, FFlowVersionId = flowVersionId, FSortOrder = 1, FStageName = "节点A",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = """{"users":[{"userId":100,"userName":"审批人A"}]}"""
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = stageDefIdB, FFlowVersionId = flowVersionId, FSortOrder = 2, FStageName = "节点B",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"审批人B"}]}""",
            FSkipDuplicateApprover = true
        });

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9804, FFlowDefinitionId = flowDefId, FFlowVersionId = flowVersionId,
            FTitle = "重提fromRejected-自身历史不去重", FStatus = "draft", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentRound = 0, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var submitResult = await engine.SubmitAsync(9804, InitiatorId);
        Assert.True(submitResult.Success, submitResult.Message);

        db.ChangeTracker.Clear();
        var approveResult = await engine.ApproveAsync(9804, approverAtStageA, new ApproveRequest { Opinion = "同意" });
        Assert.True(approveResult.Success, approveResult.Message);

        db.ChangeTracker.Clear();
        var stageBRound1 = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9804 && s.FStageDefinitionId == stageDefIdB && s.FRound == 1);
        Assert.Equal("active", stageBRound1.FStatus); // 首轮：51 与 A 的处理人(100)不重复，正常分派

        var rejectResult = await engine.RejectAsync(9804, approverAtStageB, new RejectRequest { Opinion = "不同意，打回重写" });
        Assert.True(rejectResult.Success, rejectResult.Message);

        db.ChangeTracker.Clear();
        var cardAfterReject = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9804);
        Assert.Equal("returned", cardAfterReject.FStatus);

        var resubmitResult = await engine.ResubmitAsync(9804, InitiatorId);
        Assert.True(resubmitResult.Success, resubmitResult.Message);

        db.ChangeTracker.Clear();
        // B 第二轮(round 2)实例：修复前，dedup 查询未排除"本节点自身历史"，会把上一轮 51 rejected
        // 记录也计入 actedUserIds，导致去重后无人可分派 → 自动通过（FStatus=="completed"），
        // 本次断言即为防止该回归——B 应保持 active 等待 51 真正重新审批。
        var stageBRound2 = await db.Set<CfStageInstance>().AsNoTracking()
            .SingleAsync(s => s.FCardId == 9804 && s.FStageDefinitionId == stageDefIdB && s.FRound == 2);
        Assert.Equal("active", stageBRound2.FStatus);

        var stageBRound2Assignees = await db.Set<CfStageAssignee>().AsNoTracking()
            .Where(a => a.FStageInstanceId == stageBRound2.FID)
            .ToListAsync();
        Assert.Single(stageBRound2Assignees); // 51 被真实分派，而非因自身历史被去重剔除
        Assert.Equal(approverAtStageB, stageBRound2Assignees[0].FUserId);
        Assert.Equal("pending", stageBRound2Assignees[0].FStatus);
    }

    /// <summary>复现生产全局跟踪行为的 InMemory 上下文（默认 TrackAll 会掩盖不落库 bug）。</summary>
    private static STOTOP.Infrastructure.Data.STOTOPDbContext CreateNoTrackingDb(string name)
    {
        var db = TestDbContextFactory.Create(name);
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        return db;
    }

    /// <summary>公共骨架：A→B 两个 human 节点（single 模式，fixedUsers），A 固定处理人 51；
    /// B 处理人列表 + 去重开关由调用方指定。</summary>
    private static async global::System.Threading.Tasks.Task SeedTwoStageFlowAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db,
        long flowDefId, long flowVersionId, long stageDefIdA, long stageDefIdB,
        string stageBUsersJson, bool stageBSkipDuplicateApprover)
    {
        db.Set<SysUser>().Add(new SysUser { FID = ApproverA, FName = "审批人A" });
        db.Set<SysUser>().Add(new SysUser { FID = ApproverOnlyAtB, FName = "审批人B" });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = flowDefId, FFlowName = "跨节点去重回归", FFlowCode = $"cross-stage-dedup-{flowDefId}", FOrgId = 1,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = flowVersionId, FFlowDefinitionId = flowDefId, FStatus = "published", FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = stageDefIdA, FFlowVersionId = flowVersionId, FSortOrder = 1, FStageName = "节点A",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = """{"users":[{"userId":51,"userName":"审批人A"}]}"""
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = stageDefIdB, FFlowVersionId = flowVersionId, FSortOrder = 2, FStageName = "节点B",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = stageBUsersJson,
            FSkipDuplicateApprover = stageBSkipDuplicateApprover
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
