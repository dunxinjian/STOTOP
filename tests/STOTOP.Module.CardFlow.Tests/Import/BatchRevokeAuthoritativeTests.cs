using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using STOTOP.Module.Workflow.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Import;

// 别名必须在 namespace 声明之后（命名空间级），否则外层 STOTOP.Module.Task 命名空间成员
// 优先于文件级 using 别名，简单名 Task 会被解析成命名空间报 CS0118
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// 批次撤销无工单权威路径回归：BatchRevokeHandler.RevokeBatchAsync(force=false)。
/// 产品决策——上传中心主动撤销是即时动作，不产生 WorkItem 工单。
/// 断言：批次 FIsRevoked=true 且 FStatus=Revoked、WfRevokeLog 新增一行 RevokeType="BatchRevoke"、
/// 级联生效（active 卡片被 cancelled）、FWorkItemId 保持 null（无工单）。
/// FID 用独立段（3500/9800），避开 BatchRevokeCascadeTests 的 3400/9750 段。
/// </summary>
public class BatchRevokeAuthoritativeTests
{
    private const long FlowDefId = 3500;
    private const long FlowVersionId = 3501;
    private const long BatchId = 9800;
    private const long ActiveCardId = 9801;
    private const long OperatorId = 88;

    [Fact]
    public async Task 软删除权威路径_标记撤销_写撤销日志_级联取消_无工单()
    {
        STOTOP.Infrastructure.Data.STOTOPDbContext.RegisterModuleAssembly(typeof(WfRevokeLog).Assembly);
        using var db = TestDbContextFactory.Create(nameof(BatchRevokeAuthoritativeTests));
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        await SeedBatchAsync(db);

        var handler = CreateHandler(db);
        await handler.RevokeBatchAsync(BatchId, OperatorId, force: false);

        db.ChangeTracker.Clear();

        var batch = await db.Set<CfBatch>().AsNoTracking().SingleAsync(b => b.FID == BatchId);
        Assert.True(batch.FIsRevoked);
        Assert.Equal(CfBatchStatus.Revoked, batch.FStatus);
        Assert.Equal(OperatorId, batch.FRevokedById);
        Assert.NotNull(batch.FRevokedTime);
        // 无工单：FWorkItemId 保持 null
        Assert.Null(batch.FWorkItemId);

        // 级联：active 卡片被取消
        var card = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == ActiveCardId);
        Assert.Equal("cancelled", card.FStatus);

        // 撤销日志写入一行 BatchRevoke
        var logs = await db.Set<WfRevokeLog>().AsNoTracking()
            .Where(l => l.FDataScopeId == BatchId.ToString())
            .ToListAsync();
        Assert.Single(logs);
        Assert.Equal("BatchRevoke", logs[0].FRevokeType);
        Assert.Equal("CF批次", logs[0].FTargetTable);
        Assert.True(logs[0].FIsSuccess);
    }

    private static async Task SeedBatchAsync(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = FlowDefId, FFlowName = "撤销权威回归", FFlowCode = "revoke-authoritative-regression", FOrgId = 1,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = FlowVersionId, FFlowDefinitionId = FlowDefId, FStatus = "published", FIsCurrentVersion = true
        });
        db.Set<CfBatch>().Add(new CfBatch
        {
            FID = BatchId, FFlowDefinitionId = FlowDefId, FOrgId = 1, FTriggeredById = 1,
            FTriggeredTime = DateTime.Now, FTriggerType = "fileUpload", FStatus = CfBatchStatus.Staged,
            FBatchNo = "REVOKE-AUTH-9800", FCreatedTime = DateTime.Now
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = ActiveCardId, FBatchId = BatchId, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "在途卡片", FStatus = "active", FInitiatorId = 1, FInitiatorName = "发起人",
            FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static BatchRevokeHandler CreateHandler(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        // 空 provider：卡片 FDataJson 无 voucherRef 不触发 IVoucherService；FOrchestration* 为空不触发编排回调。
        // 真实 BatchLifecycleService 提供级联段与 BumpChangeVersionAsync（InMemory 无 SEQUENCE 时容错返回 0）。
        var provider = new ServiceCollection().BuildServiceProvider();
        var lifecycle = new BatchLifecycleService(
            db,
            provider,
            new OrchestrationEngineService(db, NullLogger<OrchestrationEngineService>.Instance),
            new NoopProgressNotifier(),
            NullLogger<BatchLifecycleService>.Instance);

        var config = new ConfigurationBuilder().Build();

        return new BatchRevokeHandler(
            db,
            config,
            new NoopEventDispatcher(),
            lifecycle,
            new NoopProgressNotifier(),
            NullLogger<BatchRevokeHandler>.Instance);
    }

    private sealed class NoopEventDispatcher : IEventDispatcher
    {
        public Task PublishAsync<TEvent>(TEvent domainEvent) where TEvent : BusinessEvent => Task.CompletedTask;
    }

    private sealed class NoopProgressNotifier : IProgressNotifier
    {
        public Task NotifyImportProgressAsync(long batchId, int processedRows, int totalRows, string stage) => Task.CompletedTask;
        public Task NotifyDownloadProgressAsync(long taskId, int currentStep, int totalSteps, string stepName) => Task.CompletedTask;
        public Task NotifyProcessingProgressAsync(long ruleId, int processed, int total, string status) => Task.CompletedTask;
        public Task NotifyBatchStatusChangedAsync(long batchId, int newStatus, string statusText) => Task.CompletedTask;
        public Task NotifyBatchStatusChangedAsync(long batchId, int newStatus, string statusText, BatchSummaryDto? summary) => Task.CompletedTask;
        public Task NotifyBatchStatusChangedAsync(long batchId, int newStatus, string statusText, BatchSummaryDto? summary, long version) => Task.CompletedTask;
        public Task NotifyHomeStatsUpdatedAsync() => Task.CompletedTask;
        public Task NotifyPipelineStageAsync(long batchId, string stageName, string status, string? message = null) => Task.CompletedTask;
        public Task NotifyImportLogAsync(long batchId, string level, string message) => Task.CompletedTask;
        public Task NotifyDingTalkSyncProgressAsync(object progress) => Task.CompletedTask;
        public Task NotifyQualityAnalysisAsync(long batchId, int totalChecked, int passCount, int failCount, int dispatchedCount) => Task.CompletedTask;
        public Task NotifyExceptionDispatchedAsync(long batchId, string stageName, string errorMessage, int dispatchCount) => Task.CompletedTask;
        public Task NotifyPostPipelineStageAsync(long batchId, string stage, string status, string? message = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyDispatchItemAsync(long batchId, long dispatchResultId, string ruleName, string handlerType, int status, string? message = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyPostPipelineCompletedAsync(long batchId, int totalRules, int successCount, int failCount, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyAutoPluginStartedAsync(long batchId, string pluginName, int pluginIndex, int totalAutoPlugins = 0, List<AutoPluginStepDefinition>? steps = null) => Task.CompletedTask;
        public Task NotifyAutoPluginCompletedAsync(long batchId, string pluginName, int pluginIndex, bool success, string? message) => Task.CompletedTask;
        public Task NotifyBatchRollbackAsync(long batchId, int targetPluginIndex, List<string> rolledBackAutoPlugins) => Task.CompletedTask;
        public Task NotifyAutoPluginStepAsync(long batchId, string pluginName, int stepIndex, int totalSteps, string stepName, string status) => Task.CompletedTask;
        public Task NotifyAutoPluginDataProgressAsync(long batchId, string pluginName, int processedCount, int totalCount, string? detail = null) => Task.CompletedTask;
        public Task NotifyBatchPipelineStartedAsync(long batchId, IEnumerable<PluginSnapshot> plugins) => Task.CompletedTask;
        public Task NotifyPluginStatusChangedAsync(long batchId, int pluginIndex, string pluginName, string status, string? error = null) => Task.CompletedTask;
        public Task NotifyBatchProgressUpdateAsync(long batchId, int processedRows, int totalRows) => Task.CompletedTask;
    }
}
