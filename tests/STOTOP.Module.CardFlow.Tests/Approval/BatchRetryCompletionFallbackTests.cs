using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.CardFlow.Services.Interfaces;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Approval;

/// <summary>
/// Task 7 回归：<see cref="FlowEngineService.ProcessBatchStagesAsync"/> 在“游标之后已无可执行批次级节点”
/// (startIndex &lt; 0) 时的收尾兜底。历史实现此处直接 return；但当调用方(retry-continue 补跑 / 崩溃恢复)
/// 已先把批次置为 Processing(4) 后再进入本方法时，直接返回会让批次永久卡在“处理中”——因为收尾置 5 的
/// 逻辑(方法末尾无卡片分支)不可达。放开 retry-continue 门禁(允许对已完成批次续跑)后此缺陷会被触发，故补兜底。
/// 兜底刻意仅在 Processing 态触发，且存在未推进的草稿卡片(极窄崩溃恢复窗口)时不擅自置完成。
/// </summary>
public class BatchRetryCompletionFallbackTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task 无剩余节点且处理中_补收尾置已完成()
    {
        using var db = TestDbContextFactory.Create(nameof(无剩余节点且处理中_补收尾置已完成));
        SeedFlow(db);
        var batch = new CfBatch
        {
            FID = 8100, FFlowDefinitionId = 6200, FOrgId = 1, FStatus = CfBatchStatus.Processing,
            FCurrentBatchStageOrder = 2, // 游标已在末节点(排序2)，其后无节点 → startIndex < 0
            FTriggerType = "fileUpload", FTriggeredById = 1, FCreatedTime = DateTime.Now
        };
        db.Set<CfBatch>().Add(batch);
        await db.SaveChangesAsync();

        var recorder = new RecordingBatchLifecycleService();
        var engine = CreateEngine(db, recorder);

        await engine.ProcessBatchStagesAsync(batch);

        Assert.Contains(CfBatchStatus.Completed, recorder.Transitions);
        Assert.Equal(CfBatchStatus.Completed, batch.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 无剩余节点但非处理中_不改动状态()
    {
        using var db = TestDbContextFactory.Create(nameof(无剩余节点但非处理中_不改动状态));
        SeedFlow(db);
        var batch = new CfBatch
        {
            FID = 8101, FFlowDefinitionId = 6200, FOrgId = 1, FStatus = CfBatchStatus.Completed,
            FCurrentBatchStageOrder = 2,
            FTriggerType = "fileUpload", FTriggeredById = 1, FCreatedTime = DateTime.Now
        };
        db.Set<CfBatch>().Add(batch);
        await db.SaveChangesAsync();

        var recorder = new RecordingBatchLifecycleService();
        var engine = CreateEngine(db, recorder);

        await engine.ProcessBatchStagesAsync(batch);

        // 非 Processing 态不触发兜底：避免误动已终态/已撤销等批次
        Assert.Empty(recorder.Transitions);
        Assert.Equal(CfBatchStatus.Completed, batch.FStatus);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task 无剩余节点且处理中但存在草稿卡片_不擅自置完成()
    {
        using var db = TestDbContextFactory.Create(nameof(无剩余节点且处理中但存在草稿卡片_不擅自置完成));
        SeedFlow(db);
        var batch = new CfBatch
        {
            FID = 8102, FFlowDefinitionId = 6200, FOrgId = 1, FStatus = CfBatchStatus.Processing,
            FCurrentBatchStageOrder = 2,
            FTriggerType = "fileUpload", FTriggeredById = 1, FCreatedTime = DateTime.Now
        };
        db.Set<CfBatch>().Add(batch);
        // 未推进的草稿卡片(FanOut 已建卡但卡片级链尚未跑完的崩溃窗口) → 兜底不得谎报完成
        db.Set<CfCard>().Add(new CfCard
        {
            FID = 8900, FFlowDefinitionId = 6200, FFlowVersionId = 6201, FOrgId = 1, FBatchId = 8102,
            FStatus = "draft", FTitle = "未推进草稿", FInitiatorId = 1, FInitiatorName = "系统",
            FCurrentRound = 0, FDataJson = "{}", FCreatedTime = DateTime.Now
        });
        await db.SaveChangesAsync();

        var recorder = new RecordingBatchLifecycleService();
        var engine = CreateEngine(db, recorder);

        await engine.ProcessBatchStagesAsync(batch);

        Assert.Empty(recorder.Transitions);
        Assert.Equal(CfBatchStatus.Processing, batch.FStatus);
    }

    private static void SeedFlow(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = 6201, FFlowDefinitionId = 6200, FStatus = "published", FIsCurrentVersion = true
        });
        // 两个节点，末节点排序=2；批次游标停在 2 → 其后无节点，命中 startIndex < 0 分支
        db.Set<CfStageDefinition>().AddRange(
            new CfStageDefinition { FID = 6301, FFlowVersionId = 6201, FSortOrder = 1, FStageName = "导入", FType = "human" },
            new CfStageDefinition { FID = 6302, FFlowVersionId = 6201, FSortOrder = 2, FStageName = "自动凭证", FType = "human" });
        db.SaveChanges();
    }

    private static FlowEngineService CreateEngine(
        STOTOP.Infrastructure.Data.STOTOPDbContext db, IBatchLifecycleService lifecycle)
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
            lifecycle,
            NullLogger<FlowEngineService>.Instance);
    }
}

/// <summary>记录 TransitionBatchStatusAsync 调用并同步 batch.FStatus，供 Task 7 收尾兜底断言。</summary>
internal sealed class RecordingBatchLifecycleService : IBatchLifecycleService
{
    public List<int> Transitions { get; } = new();

    public global::System.Threading.Tasks.Task RefreshBatchStatusAsync(long batchId) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task RevokeBatchAsync(long batchId, long operatorId) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task CascadeCancelBatchArtifactsAsync(long batchId) => global::System.Threading.Tasks.Task.CompletedTask;
    public global::System.Threading.Tasks.Task<BatchProgressDto> GetBatchProgressAsync(long batchId)
        => global::System.Threading.Tasks.Task.FromResult(new BatchProgressDto());

    public global::System.Threading.Tasks.Task TransitionBatchStatusAsync(CfBatch batch, int newStatus, string? message = null)
    {
        Transitions.Add(newStatus);
        batch.FStatus = newStatus;
        return global::System.Threading.Tasks.Task.CompletedTask;
    }

    public global::System.Threading.Tasks.Task<long> BumpChangeVersionAsync(CfBatch batch) => global::System.Threading.Tasks.Task.FromResult(0L);
}
