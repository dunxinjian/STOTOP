using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

public class ExpenseApprovalE2ETests
{
    [Fact]
    public async global::System.Threading.Tasks.Task ExpenseFlow_DeptThenFinance_Approve_Completes()
    {
        using var db = TestDbContextFactory.Create(nameof(ExpenseFlow_DeptThenFinance_Approve_Completes));

        db.Set<SysUser>().AddRange(
            new SysUser { FID = 21, FName = "部门经理" },
            new SysUser { FID = 31, FName = "财务" });

        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 2263, FFlowDefinitionId = 2262, FStatus = "published", FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition
            {
                FID = 5031, FFlowVersionId = 2263, FSortOrder = 1, FStageName = "部门负责人审批",
                FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
                FAssigneeConfigJson = """{"users":[{"userId":21,"userName":"部门经理"}]}"""
            },
            new CfStageDefinition
            {
                FID = 5032, FFlowVersionId = 2263, FSortOrder = 2, FStageName = "财务审批",
                FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
                FAssigneeConfigJson = """{"users":[{"userId":31,"userName":"财务"}]}"""
            });

        var card = new CfCard
        {
            FID = 9100, FFlowDefinitionId = 2262, FFlowVersionId = 2263,
            FTitle = "费用报销", FStatus = "active", FInitiatorId = 99, FInitiatorName = "发起人",
            FCurrentStageInstanceId = 9201, FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        };
        db.Set<CfStageInstance>().Add(new CfStageInstance
        {
            FID = 9201, FCardId = 9100, FStageDefinitionId = 5031, FStageName = "部门负责人审批",
            FType = "human", FApprovalMode = "single", FRound = 1, FStatus = "active"
        });
        db.Set<CfStageAssignee>().Add(new CfStageAssignee
        {
            FID = 9301, FStageInstanceId = 9201, FUserId = 21, FUserName = "部门经理",
            FStatus = "pending", FAssignedTime = DateTime.Now
        });
        db.Set<CfTodoItem>().Add(new CfTodoItem
        {
            FID = 9401, FCardId = 9100, FStageInstanceId = 9201, FHandlerId = 21,
            FHandlerName = "部门经理", FStatus = "pending", FOrgId = 1
        });
        db.Set<CfCard>().Add(card);
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);

        await engine.ApproveAsync(9100, 21, new ApproveRequest { Opinion = "同意" });
        var afterDept = await db.Set<CfCard>().FindAsync(9100L);
        Assert.Equal("active", afterDept!.FStatus);
        var financeStage = db.Set<CfStageInstance>().Single(s => s.FCardId == 9100 && s.FStageDefinitionId == 5032);
        Assert.Equal("active", financeStage.FStatus);

        await engine.ApproveAsync(9100, 31, new ApproveRequest { Opinion = "同意" });
        var done = await db.Set<CfCard>().FindAsync(9100L);
        Assert.Equal("completed", done!.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ExpenseFlow_Submit_FromDraft_ActivatesFirstStage()
    {
        using var db = TestDbContextFactory.Create(nameof(ExpenseFlow_Submit_FromDraft_ActivatesFirstStage));

        db.Set<SysUser>().Add(new SysUser { FID = 21, FName = "部门经理" });
        // SubmitAsync 需要查询 CfFlowDefinition（步骤2）
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 2262, FFlowName = "费用报销", FFlowCode = "expense", FOrgId = 1,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 2263, FFlowDefinitionId = 2262, FStatus = "published", FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 5031, FFlowVersionId = 2263, FSortOrder = 1, FStageName = "部门负责人审批",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = """{"users":[{"userId":21,"userName":"部门经理"}]}"""
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9500, FFlowDefinitionId = 2262, FFlowVersionId = 2263,
            FTitle = "费用报销", FStatus = "draft", FInitiatorId = 99, FInitiatorName = "发起人",
            FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);
        await engine.SubmitAsync(9500, 99);

        var card = await db.Set<CfCard>().FindAsync(9500L);
        Assert.Equal("active", card!.FStatus);
        var firstStage = db.Set<CfStageInstance>().Single(s => s.FCardId == 9500 && s.FStageDefinitionId == 5031);
        Assert.Equal("active", firstStage.FStatus);
    }

    /// <summary>
    /// 回归：BatchSummary/FanOut 插件在共享 DbContext 中 Add+SaveChanges 创建卡片后，
    /// 卡片仍处于 ChangeTracker 跟踪态；生产环境全局 NoTrackingWithIdentityResolution 下，
    /// ProcessCardStagesForBatchAsync 的无跟踪重查询会得到新实例，随后
    /// _dbContext.Entry(card).State = Modified 与已跟踪实例撞 key，
    /// 抛 "CfCard cannot be tracked because another instance with the same key value is already being tracked"，
    /// 批次卡在 CardCreated(3) 无法到终态。
    ///
    /// 注意：测试 DbContext 默认 TrackAll（重查询会返回已跟踪实例从而掩盖 bug），
    /// 必须显式切到 NoTrackingWithIdentityResolution 才能复现生产行为（见 Program.cs:112）。
    /// </summary>
    [Fact]
    public async global::System.Threading.Tasks.Task BatchSummaryCard_AdvanceToCardStage_UnderNoTrackingResolution_DoesNotThrowIdentityConflict()
    {
        using var db = TestDbContextFactory.Create(
            nameof(BatchSummaryCard_AdvanceToCardStage_UnderNoTrackingResolution_DoesNotThrowIdentityConflict));
        // 复现生产全局跟踪行为：无此行，测试用默认 TrackAll 不会触发冲突。
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;

        db.Set<SysUser>().Add(new SysUser { FID = 41, FName = "确认人" });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 2262, FFlowName = "申通总部交易明细", FFlowCode = "st-hq-tx", FOrgId = 192,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 2263, FFlowDefinitionId = 2262, FStatus = "published", FIsCurrentVersion = true
        });
        // 仅一个卡片级 human 节点「确认通知」——无批次级节点，
        // 故 ProcessBatchStagesAsync 的批次级链为空，直接进入卡片级推进。
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 5031, FFlowVersionId = 2263, FSortOrder = 1, FStageName = "确认通知",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = """{"users":[{"userId":41,"userName":"确认人"}]}"""
        });
        var batch = new CfBatch
        {
            FID = 7001, FFlowDefinitionId = 2262, FOrgId = 192, FStatus = 3, // 3=已创建卡片
            FTriggerType = "fileUpload", FTotalRows = 100, FSuccessRows = 100,
            FTriggeredById = 1, FCreatedTime = DateTime.Now
        };
        db.Set<CfBatch>().Add(batch);
        await db.SaveChangesAsync();

        // 让参考数据与生产一致地"未跟踪"，仅让汇总卡处于跟踪态。
        db.ChangeTracker.Clear();

        // 模拟 BatchSummaryPlugin：在共享 DbContext 中 Add 一张 draft 汇总卡 + SaveChanges，
        // 保存后该实例仍处于 Tracked(Unchanged) —— 这正是 bug 的前置条件。
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 9100, FFlowDefinitionId = 2262, FFlowVersionId = 2263, FOrgId = 192,
            FBatchId = 7001, FTitle = "导入汇总", FStatus = "draft",
            FInitiatorId = 1, FInitiatorName = "系统", FCurrentRound = 0,
            FDataJson = "{}", FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);

        // 修复前在此抛 InvalidOperationException（identity conflict）；修复后正常推进。
        await engine.ProcessBatchStagesAsync(batch);

        db.ChangeTracker.Clear();
        var advanced = await db.Set<CfCard>().FindAsync(9100L);
        Assert.NotNull(advanced);
        Assert.Equal("active", advanced!.FStatus);
        var stage = db.Set<CfStageInstance>().Single(s => s.FCardId == 9100);
        Assert.Equal("确认通知", stage.FStageName);
        Assert.Equal("active", stage.FStatus);
    }

    /// <summary>
    /// 同上回归，覆盖 FanOut「多卡」场景：FanOutPlugin 在共享 DbContext 里 Add 多张卡并 SaveChanges，
    /// 全部留在跟踪态；ProcessCardStagesForBatchAsync 逐卡推进时，循环里每张卡的
    /// _dbContext.Entry(card).State=Modified 都不得与已跟踪实例撞 key。
    /// 确保修复(AsTracking 复用已跟踪实例)在多实例 + 标识解析下对每张卡都成立、互不串扰。
    /// </summary>
    [Fact]
    public async global::System.Threading.Tasks.Task FanOutCards_MultiTracked_AdvanceToCardStage_UnderNoTrackingResolution_NoIdentityConflict()
    {
        using var db = TestDbContextFactory.Create(
            nameof(FanOutCards_MultiTracked_AdvanceToCardStage_UnderNoTrackingResolution_NoIdentityConflict));
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;

        db.Set<SysUser>().Add(new SysUser { FID = 41, FName = "确认人" });
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = 2272, FFlowName = "申通出港运单", FFlowCode = "st-outbound", FOrgId = 192,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 2273, FFlowDefinitionId = 2272, FStatus = "published", FIsCurrentVersion = true
        });
        db.Set<CfStageDefinition>().Add(new CfStageDefinition
        {
            FID = 5041, FFlowVersionId = 2273, FSortOrder = 1, FStageName = "确认通知",
            FType = "human", FApprovalMode = "single", FAssigneeStrategy = "fixedUsers",
            FAssigneeConfigJson = """{"users":[{"userId":41,"userName":"确认人"}]}"""
        });
        var batch = new CfBatch
        {
            FID = 7002, FFlowDefinitionId = 2272, FOrgId = 192, FStatus = 3,
            FTriggerType = "fileUpload", FTotalRows = 3, FSuccessRows = 3,
            FTriggeredById = 1, FCreatedTime = DateTime.Now
        };
        db.Set<CfBatch>().Add(batch);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        // 模拟 FanOutPlugin：多张 draft 卡 Add+SaveChanges，全部留在 Tracked(Unchanged)。
        var cardIds = new[] { 9101L, 9102L, 9103L };
        foreach (var fid in cardIds)
        {
            db.Set<CfCard>().Add(new CfCard
            {
                FID = fid, FFlowDefinitionId = 2272, FFlowVersionId = 2273, FOrgId = 192,
                FBatchId = 7002, FTitle = $"出港卡{fid}", FStatus = "draft",
                FInitiatorId = 1, FInitiatorName = "系统", FCurrentRound = 0,
                FDataJson = "{}", FCreatedTime = DateTime.Now
            });
        }
        await db.SaveChangesAsync();

        var engine = CreateEngine(db);

        await engine.ProcessBatchStagesAsync(batch);

        db.ChangeTracker.Clear();
        foreach (var fid in cardIds)
        {
            var c = await db.Set<CfCard>().FindAsync(fid);
            Assert.NotNull(c);
            Assert.Equal("active", c!.FStatus);
            Assert.True(
                db.Set<CfStageInstance>().Any(s => s.FCardId == fid && s.FStatus == "active"),
                $"卡 {fid} 应已激活「确认通知」节点实例");
        }
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
            new ConditionEvaluator(),
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
