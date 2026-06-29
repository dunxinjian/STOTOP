using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Validation;
using STOTOP.Module.Finance.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Validation;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

public class VoucherValidationAnalyzerTests
{
    private static ValidationBatchContext CreateContext() => new()
    {
        BatchId = 81,
        FlowDefinitionId = 100,
        OrgId = 192,
        TotalRows = 10,
        BatchStatus = 5,
        Tolerance = 0.01m
    };

    private static void SeedVoucher(
        Infrastructure.Data.STOTOPDbContext db,
        long voucherId,
        decimal debit,
        decimal credit)
    {
        db.Add(new FinVoucher
        {
            FID = voucherId,
            FVoucherWord = "记",
            FVoucherNo = (int)voucherId,
            FDate = new DateTime(2026, 6, 1),
            FPeriodId = 1,
            FAccountSetId = 1,
            FOrgId = 192,
            FCreator = "test"
        });
        db.Add(new FinVoucherEntry
        {
            FID = voucherId * 10 + 1, FVoucherId = voucherId, FLineNo = 1,
            FSummary = "借方", FAccountId = 1, FAccountCode = "6601", FAccountName = "销售费用",
            FDebitAmount = debit, FCreditAmount = 0, FOrgId = 192
        });
        db.Add(new FinVoucherEntry
        {
            FID = voucherId * 10 + 2, FVoucherId = voucherId, FLineNo = 2,
            FSummary = "贷方", FAccountId = 2, FAccountCode = "1001", FAccountName = "库存现金",
            FDebitAmount = 0, FCreditAmount = credit, FOrgId = 192
        });
    }

    [Fact]
    public async Task Analyze_reports_blocker_for_unbalanced_actual_vouchers()
    {
        await using var db = TestDbContextFactory.Create(nameof(Analyze_reports_blocker_for_unbalanced_actual_vouchers));
        db.Add(new CfVoucherRecord
        {
            FID = 1,
            FBatchId = 81,
            FTargetTable = "STG测试表",
            FTotalRows = 10,
            FMatchedRows = 10,
            FUnmatchedRows = 0,
            FGeneratedVoucherCount = 2,
            FVoucherIdsJson = "[7001,7002]",
            FStatus = 1
        });
        SeedVoucher(db, 7001, debit: 100m, credit: 100m);   // 平衡
        SeedVoucher(db, 7002, debit: 100m, credit: 99.50m); // 不平
        await db.SaveChangesAsync();

        var analyzer = new VoucherValidationAnalyzer(db, new ValidationAttributionClassifier());
        var findings = await analyzer.AnalyzeAsync(CreateContext(), new ImportValidationRunRequest());

        var finding = Assert.Single(findings, f => f.Title == "实际凭证借贷不平");
        Assert.Equal(ValidationSeverity.Blocker, finding.Severity);
        Assert.Equal(ValidationAttribution.CalculationLogic, finding.Attribution);
        Assert.Equal(1, finding.AffectedRows);
        Assert.Equal(7002, finding.VoucherId);
        Assert.Equal(0.50m, finding.Difference);
    }

    [Fact]
    public async Task Analyze_passes_when_all_vouchers_balanced()
    {
        await using var db = TestDbContextFactory.Create(nameof(Analyze_passes_when_all_vouchers_balanced));
        db.Add(new CfVoucherRecord
        {
            FID = 1,
            FBatchId = 81,
            FTargetTable = "STG测试表",
            FTotalRows = 10,
            FMatchedRows = 10,
            FUnmatchedRows = 0,
            FGeneratedVoucherCount = 1,
            FVoucherIdsJson = "[7001]",
            FStatus = 1
        });
        SeedVoucher(db, 7001, debit: 100m, credit: 100m);
        await db.SaveChangesAsync();

        var analyzer = new VoucherValidationAnalyzer(db, new ValidationAttributionClassifier());
        var findings = await analyzer.AnalyzeAsync(CreateContext(), new ImportValidationRunRequest());

        Assert.DoesNotContain(findings, f => f.Title == "实际凭证借贷不平");
    }
}
