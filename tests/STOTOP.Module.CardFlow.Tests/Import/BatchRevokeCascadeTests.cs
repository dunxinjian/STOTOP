using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Import;

// 别名必须在 namespace 声明之后（命名空间级），否则外层 STOTOP.Module.Task 命名空间成员
// 优先于文件级 using 别名，简单名 Task 会被解析成命名空间报 CS0118
using Task = global::System.Threading.Tasks.Task;

/// <summary>
/// 撤销级联段回归：BatchLifecycleService.CascadeCancelBatchArtifactsAsync
/// （上传中心软删 RevokeCfBatchAsync 与权威路径 BatchRevokeHandler 共用）。
/// 断言：未完成卡片被取消、已完成卡片不动、行明细置 5(已撤销) 且 4(已忽略) 例外。
/// 生产全局 NoTrackingWithIdentityResolution，测试显式复现该行为验证真实落库。
/// </summary>
public class BatchRevokeCascadeTests
{
    private const long FlowDefId = 3400;
    private const long FlowVersionId = 3401;
    private const long BatchId = 9750;
    private const long ActiveCardId = 9751;
    private const long CompletedCardId = 9752;
    private const long PendingRowId = 9761;
    private const long IgnoredRowId = 9762;
    private const long CardCreatedRowId = 9763;

    [Fact]
    public async Task 级联取消_未完成卡片取消_完成卡片不动_行置5且忽略行例外()
    {
        using var db = TestDbContextFactory.Create(nameof(BatchRevokeCascadeTests));
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        await SeedBatchAsync(db);

        var service = CreateService(db);
        await service.CascadeCancelBatchArtifactsAsync(BatchId);

        db.ChangeTracker.Clear();

        var activeCard = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == ActiveCardId);
        Assert.Equal("cancelled", activeCard.FStatus);
        Assert.NotNull(activeCard.FCompletedTime);

        var completedCard = await db.Set<CfCard>().AsNoTracking().SingleAsync(c => c.FID == CompletedCardId);
        Assert.Equal("completed", completedCard.FStatus);

        var pendingRow = await db.Set<CfBatchRow>().AsNoTracking().SingleAsync(r => r.FID == PendingRowId);
        Assert.Equal(5, pendingRow.FStatus);

        var cardCreatedRow = await db.Set<CfBatchRow>().AsNoTracking().SingleAsync(r => r.FID == CardCreatedRowId);
        Assert.Equal(5, cardCreatedRow.FStatus);

        // 4=已忽略 不参与撤销级联
        var ignoredRow = await db.Set<CfBatchRow>().AsNoTracking().SingleAsync(r => r.FID == IgnoredRowId);
        Assert.Equal(4, ignoredRow.FStatus);
    }

    private static async Task SeedBatchAsync(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        db.Set<CfFlowDefinition>().Add(new CfFlowDefinition
        {
            FID = FlowDefId, FFlowName = "撤销级联回归", FFlowCode = "revoke-cascade-regression", FOrgId = 1,
            FStatus = "published", FCreatorId = 1, FCreatedTime = DateTime.Now
        });
        db.Set<CfFlowVersion>().Add(new CfFlowVersion
        {
            FID = FlowVersionId, FFlowDefinitionId = FlowDefId, FStatus = "published", FIsCurrentVersion = true
        });
        db.Set<CfBatch>().Add(new CfBatch
        {
            FID = BatchId, FFlowDefinitionId = FlowDefId, FOrgId = 1, FTriggeredById = 1,
            FTriggeredTime = DateTime.Now, FTriggerType = "fileUpload", FStatus = 4,
            FBatchNo = "REVOKE-CASCADE-9750", FCreatedTime = DateTime.Now
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = ActiveCardId, FBatchId = BatchId, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "在途卡片", FStatus = "active", FInitiatorId = 1, FInitiatorName = "发起人",
            FCurrentRound = 1, FOrgId = 1, FDataJson = "{}"
        });
        db.Set<CfCard>().Add(new CfCard
        {
            FID = CompletedCardId, FBatchId = BatchId, FFlowDefinitionId = FlowDefId, FFlowVersionId = FlowVersionId,
            FTitle = "已完成卡片", FStatus = "completed", FInitiatorId = 1, FInitiatorName = "发起人",
            FCurrentRound = 1, FOrgId = 1, FDataJson = "{}", FCompletedTime = DateTime.Now
        });
        db.Set<CfBatchRow>().AddRange(
            new CfBatchRow { FID = PendingRowId, FBatchId = BatchId, FRowNo = 1, FDataJson = "{}", FStatus = 0, FCreatedTime = DateTime.Now },
            new CfBatchRow { FID = IgnoredRowId, FBatchId = BatchId, FRowNo = 2, FDataJson = "{}", FStatus = 4, FCreatedTime = DateTime.Now },
            new CfBatchRow { FID = CardCreatedRowId, FBatchId = BatchId, FRowNo = 3, FDataJson = "{}", FStatus = 3, FCardId = ActiveCardId, FCreatedTime = DateTime.Now });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static BatchLifecycleService CreateService(STOTOP.Infrastructure.Data.STOTOPDbContext db)
    {
        // 空 provider：卡片 FDataJson 无 voucherRef 不触发 IVoucherService 解析；FOrchestration* 为空不触发编排回调
        var provider = new ServiceCollection().BuildServiceProvider();
        return new BatchLifecycleService(
            db,
            provider,
            new OrchestrationEngineService(db, NullLogger<OrchestrationEngineService>.Instance),
            new NoopProgressNotifier(),
            NullLogger<BatchLifecycleService>.Instance);
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
