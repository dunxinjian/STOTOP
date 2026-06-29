using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Validation;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Validation;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

public class ImportCalculationValidationServiceTests
{
    private static ImportCalculationValidationService CreateService(
        Infrastructure.Data.STOTOPDbContext db,
        IPricingExplainProvider? explainProvider = null)
    {
        var classifier = new ValidationAttributionClassifier();
        return new ImportCalculationValidationService(
            db,
            new VoucherValidationAnalyzer(db, classifier),
            new PricingValidationAnalyzer(db, classifier),
            new CostValidationAnalyzer(db, classifier),
            pricingExplainProvider: explainProvider);
    }

    private static CfBatch CreateBatch(long id, int status, bool revoked = false) => new()
    {
        FID = id,
        FFlowDefinitionId = 100,
        FOrgId = 192,
        FBatchNo = $"B{id}",
        FTriggerType = "fileUpload",
        FActualTargetTable = "STG测试表",
        FTotalRows = 10,
        FStatus = status,
        FIsRevoked = revoked,
        FCurrentNodeName = status < 5 ? "出港运单价格计算" : null,
        FProgressPercent = status < 5 ? 40 : 100,
        FTriggeredTime = new DateTime(2026, 6, 1),
        FCreatedTime = new DateTime(2026, 6, 1)
    };

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(4, false, true)]
    [InlineData(5, false, false)]
    [InlineData(4, true, false)] // 已回撤不算执行中
    public void ValidationBatchContext_IsBatchRunning_follows_status_machine(int status, bool revoked, bool expected)
    {
        var context = new ValidationBatchContext
        {
            BatchStatus = status,
            IsRevoked = revoked
        };

        Assert.Equal(expected, context.IsBatchRunning);
    }

    [Fact]
    public async Task GetSummaryAsync_returns_batch_execution_state()
    {
        await using var db = TestDbContextFactory.Create(nameof(GetSummaryAsync_returns_batch_execution_state));
        db.Add(CreateBatch(81, status: 4));
        db.Add(new CfFlowDefinition { FID = 100, FFlowName = "申通出港运单导入", FFlowCode = "EXP_IMPORT", FOrgId = 192 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var summary = await service.GetSummaryAsync(81, 192);

        Assert.Equal(4, summary.BatchStatus);
        Assert.Equal("处理中", summary.BatchStatusText);
        Assert.True(summary.IsBatchRunning);
        Assert.Equal("出港运单价格计算", summary.CurrentNodeName);
        Assert.Equal(40, summary.ProgressPercent);
        Assert.Equal("申通出港运单导入", summary.FlowName);
    }

    [Fact]
    public async Task RunAsync_injects_blocker_finding_while_batch_is_running()
    {
        await using var db = TestDbContextFactory.Create(nameof(RunAsync_injects_blocker_finding_while_batch_is_running));
        db.Add(CreateBatch(81, status: 4));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var report = await service.RunAsync(81, 192, new ImportValidationRunRequest());

        Assert.True(report.IsBatchRunning);
        Assert.Equal("处理中", report.BatchStatusText);

        var runningFinding = Assert.Single(report.Findings, f => f.Title == "批次仍在执行中");
        Assert.Equal(ValidationSeverity.Blocker, runningFinding.Severity);
        Assert.Equal(ValidationAttribution.Persistence, runningFinding.Attribution);
        Assert.Equal(10, runningFinding.AffectedRows);
    }

    [Fact]
    public async Task RunAsync_does_not_inject_running_finding_for_completed_batch()
    {
        await using var db = TestDbContextFactory.Create(nameof(RunAsync_does_not_inject_running_finding_for_completed_batch));
        db.Add(CreateBatch(81, status: 5));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var report = await service.RunAsync(81, 192, new ImportValidationRunRequest());

        Assert.False(report.IsBatchRunning);
        Assert.Equal("已完成", report.BatchStatusText);
        Assert.DoesNotContain(report.Findings, f => f.Title == "批次仍在执行中");
    }

    [Theory]
    [InlineData("all")]
    [InlineData("allLimited")]
    [InlineData("errorsOnly")]
    [InlineData("sample")]
    public async Task RunAsync_accepts_all_documented_modes(string mode)
    {
        await using var db = TestDbContextFactory.Create($"{nameof(RunAsync_accepts_all_documented_modes)}_{mode}");
        db.Add(CreateBatch(81, status: 5));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var report = await service.RunAsync(81, 192, new ImportValidationRunRequest { Mode = mode });

        Assert.NotNull(report);
        Assert.Equal(81, report.BatchId);
    }

    [Fact]
    public async Task RunAsync_swallows_explain_provider_failures()
    {
        await using var db = TestDbContextFactory.Create(nameof(RunAsync_swallows_explain_provider_failures));
        db.Add(CreateBatch(81, status: 5));
        await db.SaveChangesAsync();

        var service = CreateService(db, new ThrowingExplainProvider());
        var report = await service.RunAsync(81, 192, new ImportValidationRunRequest { IncludeEvidence = true });

        Assert.NotNull(report);
    }

    private sealed class ThrowingExplainProvider : IPricingExplainProvider
    {
        public Task<IReadOnlyDictionary<string, PricingExplainSnapshot>> ExplainAsync(
            PricingExplainRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }
}
