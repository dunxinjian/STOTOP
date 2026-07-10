using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

/// <summary>
/// M8-C 件④：会签比例(ratio)审批模式 + orsign 退回 bug 修复回归。
/// 前半段为 ApprovalModeHandler 纯逻辑单测（ratio 完成/退回阈值判定 + threshold 缺省回退）；
/// 后半段为 FlowEngineService 集成测试（NoTracking 落库 + orsign/ratio 多处理人 reject 分支）。
/// </summary>
public class ApprovalRatioTests
{
    // ── ApprovalModeHandler: ratio 完成判定 ──────────────────────────────

    [Fact]
    public void Ratio_Completes_WhenApprovedRatioMeetsThreshold()
    {
        var handler = new ApprovalModeHandler();
        var assignees = new List<AssigneeStatus>
        {
            new(1, "approved"),
            new(2, "approved"),
            new(3, "pending")
        };

        // 2/3 ≈ 66.7% >= 60%
        Assert.True(handler.IsStageCompleted("ratio", assignees, 60));
    }

    [Fact]
    public void Ratio_DoesNotComplete_WhenApprovedRatioBelowThreshold()
    {
        var handler = new ApprovalModeHandler();
        var assignees = new List<AssigneeStatus>
        {
            new(1, "approved"),
            new(2, "pending"),
            new(3, "pending")
        };

        // 1/3 ≈ 33.3% < 60%
        Assert.False(handler.IsStageCompleted("ratio", assignees, 60));
    }

    // ── ApprovalModeHandler: ratio 退回判定（补数驳回） ──────────────────

    [Fact]
    public void Ratio_Returns_WhenRejectedRatioExceedsComplement()
    {
        var handler = new ApprovalModeHandler();
        var assignees = new List<AssigneeStatus>
        {
            new(1, "rejected"),
            new(2, "rejected"),
            new(3, "pending")
        };

        // 2/3 ≈ 66.7% > (100-60)/100 = 40%
        Assert.True(handler.IsStageReturned("ratio", assignees, 60));
    }

    [Fact]
    public void Ratio_DoesNotReturn_WhenRejectedRatioWithinComplement()
    {
        var handler = new ApprovalModeHandler();
        var assignees = new List<AssigneeStatus>
        {
            new(1, "rejected"),
            new(2, "pending"),
            new(3, "pending")
        };

        // 1/3 ≈ 33.3% <= 40%
        Assert.False(handler.IsStageReturned("ratio", assignees, 60));
    }

    // ── ApprovalModeHandler: threshold 缺省/越界回退 100%（=countersign 语义） ──

    [Fact]
    public void Ratio_FallsBackTo100Percent_WhenThresholdNull()
    {
        var handler = new ApprovalModeHandler();
        var twoOfThree = new List<AssigneeStatus>
        {
            new(1, "approved"),
            new(2, "approved"),
            new(3, "pending")
        };
        var allApproved = new List<AssigneeStatus>
        {
            new(1, "approved"),
            new(2, "approved"),
            new(3, "approved")
        };

        Assert.False(handler.IsStageCompleted("ratio", twoOfThree, null));
        Assert.True(handler.IsStageCompleted("ratio", allApproved, null));

        var oneRejected = new List<AssigneeStatus>
        {
            new(1, "rejected"),
            new(2, "pending"),
            new(3, "pending")
        };
        // fallback 100% → 补数=0%，任一 rejected 即退回（countersign 语义）
        Assert.True(handler.IsStageReturned("ratio", oneRejected, null));
    }

    [Fact]
    public void Ratio_FallsBackTo100Percent_WhenThresholdOutOfRange()
    {
        var handler = new ApprovalModeHandler();
        var twoOfThree = new List<AssigneeStatus>
        {
            new(1, "approved"),
            new(2, "approved"),
            new(3, "pending")
        };

        Assert.False(handler.IsStageCompleted("ratio", twoOfThree, 0));
        Assert.False(handler.IsStageCompleted("ratio", twoOfThree, 100));
        Assert.False(handler.IsStageCompleted("ratio", twoOfThree, -5));
    }

    // ── FlowEngineService 集成：orsign 退回 bug 修复 ─────────────────────

    private const long FlowDefId = 3400;
    private const long FlowVersionId = 3401;
    private const long ApproverA = 61;
    private const long ApproverB = 62;
    private const long ApproverC = 63;
    private const long InitiatorId = 88;

    [Fact]
    public async global::System.Threading.Tasks.Task OrsignReject_PartialReject_DoesNotReturnCard_WaitsForOthers()
    {
        const long stageDefId = 6401;
        using var db = CreateNoTrackingDb(nameof(OrsignReject_PartialReject_DoesNotReturnCard_WaitsForOthers));
        await SeedThreeAssigneeStageAsync(db, stageDefId, "orsign", threshold: null);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9711, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "orsign部分驳回", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9811, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9811, FCardId = 9711, FStageDefinitionId = stageDefId, FStageName = "会审",
            FType = "human", FApprovalMode = "orsign", FRound = 1, FStatus = "active"
        });
        AddThreeAssignees(db, 9811, 9911);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        // 修复前：任一 reject 即硬编码整卡 returned；修复后 orsign 需全部 rejected 才退回
        var result = await engine.RejectAsync(9711, ApproverA, new RejectRequest { Opinion = "有异议" });
        Assert.True(result.Success, result.Message);
        Assert.Equal("active", result.NewStatus);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9711);
        Assert.Equal("active", card.FStatus);
        Assert.Equal(9811, card.FCurrentStageInstanceId);
        var stage = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == 9811);
        Assert.Equal("active", stage.FStatus);
        var rejectedAssignee = await db.Set<CfStageAssignee>().AsNoTracking().SingleAsync(a => a.FID == 9911);
        Assert.Equal("rejected", rejectedAssignee.FStatus);
        var stillPending = await db.Set<CfStageAssignee>().AsNoTracking().SingleAsync(a => a.FID == 9912);
        Assert.Equal("pending", stillPending.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task OrsignReject_AllReject_ReturnsCard()
    {
        const long stageDefId = 6402;
        using var db = CreateNoTrackingDb(nameof(OrsignReject_AllReject_ReturnsCard));
        await SeedThreeAssigneeStageAsync(db, stageDefId, "orsign", threshold: null);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9712, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "orsign全部驳回", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9812, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9812, FCardId = 9712, FStageDefinitionId = stageDefId, FStageName = "会审",
            FType = "human", FApprovalMode = "orsign", FRound = 1, FStatus = "active"
        });
        AddThreeAssignees(db, 9812, 9921);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var r1 = await engine.RejectAsync(9712, ApproverA, new RejectRequest { Opinion = "驳回1" });
        Assert.True(r1.Success, r1.Message);
        Assert.Equal("active", r1.NewStatus);

        var r2 = await engine.RejectAsync(9712, ApproverB, new RejectRequest { Opinion = "驳回2" });
        Assert.True(r2.Success, r2.Message);
        Assert.Equal("active", r2.NewStatus);

        var r3 = await engine.RejectAsync(9712, ApproverC, new RejectRequest { Opinion = "驳回3" });
        Assert.True(r3.Success, r3.Message);
        Assert.Equal("returned", r3.NewStatus);

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9712);
        Assert.Equal("returned", card.FStatus);
        Assert.Null(card.FCurrentStageInstanceId);
        var stage = await db.Set<CfStageInstance>().AsNoTracking().SingleAsync(s => s.FID == 9812);
        Assert.Equal("returned", stage.FStatus);
    }

    // ── FlowEngineService 集成：ratio 退回 ────────────────────────────────

    [Fact]
    public async global::System.Threading.Tasks.Task RatioReject_TwoOfThreeReject_ReturnsCard()
    {
        const long stageDefId = 6403;
        using var db = CreateNoTrackingDb(nameof(RatioReject_TwoOfThreeReject_ReturnsCard));
        await SeedThreeAssigneeStageAsync(db, stageDefId, "ratio", threshold: 60);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9713, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "ratio驳回", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9813, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9813, FCardId = 9713, FStageDefinitionId = stageDefId, FStageName = "比例会审",
            FType = "human", FApprovalMode = "ratio", FRound = 1, FStatus = "active"
        });
        AddThreeAssignees(db, 9813, 9931);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var r1 = await engine.RejectAsync(9713, ApproverA, new RejectRequest { Opinion = "驳回1" });
        Assert.True(r1.Success, r1.Message);
        Assert.Equal("active", r1.NewStatus); // 1/3 ≈ 33% <= 40% 补数，未达退回

        var r2 = await engine.RejectAsync(9713, ApproverB, new RejectRequest { Opinion = "驳回2" });
        Assert.True(r2.Success, r2.Message);
        Assert.Equal("returned", r2.NewStatus); // 2/3 ≈ 67% > 40% 补数，达到退回

        db.ChangeTracker.Clear();
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9713);
        Assert.Equal("returned", card.FStatus);
    }

    // ── FlowEngineService 集成：ratio 通过 ────────────────────────────────

    [Fact]
    public async global::System.Threading.Tasks.Task RatioApprove_TwoOfThreeApprove_CompletesCard()
    {
        const long stageDefId = 6404;
        using var db = CreateNoTrackingDb(nameof(RatioApprove_TwoOfThreeApprove_CompletesCard));
        await SeedThreeAssigneeStageAsync(db, stageDefId, "ratio", threshold: 60);

        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9714, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "ratio通过", FStatus = "active", FInitiatorId = InitiatorId, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9814, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9814, FCardId = 9714, FStageDefinitionId = stageDefId, FStageName = "比例会审",
            FType = "human", FApprovalMode = "ratio", FRound = 1, FStatus = "active"
        });
        AddThreeAssignees(db, 9814, 9941);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var engine = CreateEngine(db);
        var r1 = await engine.ApproveAsync(9714, ApproverA, new ApproveRequest { Opinion = "同意1" });
        Assert.True(r1.Success, r1.Message);

        db.ChangeTracker.Clear();
        var cardAfterFirst = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9714);
        Assert.Equal("active", cardAfterFirst.FStatus); // 1/3 < 60%，尚未完成

        var r2 = await engine.ApproveAsync(9714, ApproverB, new ApproveRequest { Opinion = "同意2" });
        Assert.True(r2.Success, r2.Message);

        db.ChangeTracker.Clear();
        var cardAfterSecond = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == 9714);
        // 单节点流程：2/3 >= 60% 达到通过阈值 → 节点完成 → 无下一节点 → 卡片整体完成
        Assert.Equal("completed", cardAfterSecond.FStatus);
    }

    /// <summary>公共骨架：单 human 节点、3 固定处理人（fixedUsers），供 orsign/ratio 用例复用。</summary>
    private static async global::System.Threading.Tasks.Task SeedThreeAssigneeStageAsync(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, long stageDefId, string approvalMode, int? threshold)
    {
        db.Set<SysUser>().Add(new SysUser { FID = ApproverA, FName = "审批人A" });
        db.Set<SysUser>().Add(new SysUser { FID = ApproverB, FName = "审批人B" });
        db.Set<SysUser>().Add(new SysUser { FID = ApproverC, FName = "审批人C" });
        if (!await db.Set<CfFlowDefinition>().AnyAsync(f => f.FID == FlowDefId))
        {
            db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
            {
                FID = FlowDefId, FFlowName = "比例会签回归", FFlowCode = "ratio-regression", FOrgId = 1,
                FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
            });
            db.Set<CfFlowVersion>().Add(new CfFlowVersion
            {
                FID = FlowVersionId, FFlowDefinitionId = FlowDefId, FStatus = "published", FIsCurrentVersion = true
            });
        }
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = stageDefId, FFlowVersionId = FlowVersionId, FSortOrder = 1, FStageName = "会审",
            FType = "human", FApprovalMode = approvalMode, FAssigneeStrategy = "fixedUsers",
            FApprovalThreshold = threshold,
            FAssigneeConfigJson = """{"users":[{"userId":61,"userName":"审批人A"},{"userId":62,"userName":"审批人B"},{"userId":63,"userName":"审批人C"}]}"""
        });
        await db.SaveChangesAsync();
    }

    private static void AddThreeAssignees(STOTOP.Infrastructure.Data.STOTOPDbContext db, long stageInstanceId, long baseAssigneeId)
    {
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = baseAssigneeId, FStageInstanceId = stageInstanceId, FUserId = ApproverA, FUserName = "审批人A",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = baseAssigneeId + 1, FStageInstanceId = stageInstanceId, FUserId = ApproverB, FUserName = "审批人B",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = baseAssigneeId + 2, FStageInstanceId = stageInstanceId, FUserId = ApproverC, FUserName = "审批人C",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
    }

    /// <summary>复现生产全局跟踪行为的 InMemory 上下文（默认 TrackAll 会掩盖不落库 bug）。</summary>
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
