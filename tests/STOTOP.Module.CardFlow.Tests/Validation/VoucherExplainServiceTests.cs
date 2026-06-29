using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Validation;
using STOTOP.Module.Finance.Entities;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Validation;

// STOTOP.Module.Task / STOTOP.Module.System 会遮蔽 BCL 命名空间，命名空间内 alias 恢复
using Task = global::System.Threading.Tasks.Task;

public class VoucherExplainServiceTests
{
    private const long FlowDefinitionId = 100;
    private const long BatchId = 81;
    private const long OrgId = 192;

    private static ValidationBatchContext CreateContext(int batchStatus = 5) => new()
    {
        BatchId = BatchId,
        FlowDefinitionId = FlowDefinitionId,
        OrgId = OrgId,
        TotalRows = 10,
        BatchStatus = batchStatus,
        Tolerance = 0.01m
    };

    /// <summary>ROW 聚合、Layer2 分类关键词、借贷各一条兜底分录、keyFields=[F单号]</summary>
    private static RulesBasedVoucherConfigV2 CreateConfig(
        string aggregation = "ROW",
        string? creditAmountField = null)
    {
        return new RulesBasedVoucherConfigV2
        {
            VoucherWord = "记",
            DateField = "F业务日期",
            StagingTable = "STG测试表",
            AccountSetId = 1,
            KeyFields = ["F单号"],
            MatchingLayers = new MatchingLayerConfig
            {
                ExactMatchField = "F费用编码",
                CategoryField = "F费用类别",
                SummaryField = "F交易摘要"
            },
            RuleGroups =
            [
                new RuleGroupV2
                {
                    Id = "group-freight",
                    Name = "运输费",
                    Order = 1,
                    CategoryKeywords = ["快递", "物流"],
                    AmountAggregation = aggregation,
                    Lines =
                    [
                        new EntryLineV2
                        {
                            LineNo = 1,
                            Direction = "借",
                            AccountId = 1001,
                            AccountCode = "6601",
                            AmountField = "F金额",
                            SummaryTemplate = "运费-{F网点名称}",
                            DisplayOrder = 1,
                            Status = 1
                        },
                        new EntryLineV2
                        {
                            LineNo = 2,
                            Direction = "贷",
                            AccountId = 2001,
                            AccountCode = "1001",
                            AmountField = creditAmountField ?? "F金额",
                            DisplayOrder = 2,
                            Status = 1
                        }
                    ]
                },
                new RuleGroupV2
                {
                    Id = "group-conditional",
                    Name = "条件组",
                    Order = 2,
                    CategoryKeywords = ["条件"],
                    AmountAggregation = "ROW",
                    Fallthrough = false,
                    Lines =
                    [
                        new EntryLineV2
                        {
                            LineNo = 1,
                            Direction = "借",
                            AccountId = 1002,
                            AmountField = "F金额",
                            ConditionField = "F费用类型",
                            ConditionValues = ["EMS"],
                            DisplayOrder = 1,
                            Status = 1
                        }
                    ]
                }
            ]
        };
    }

    private static async Task SeedFlowWithConfigAsync(
        Infrastructure.Data.STOTOPDbContext db,
        RulesBasedVoucherConfigV2 config)
    {
        db.Add(new CfAutoPluginRegistry
        {
            FID = 5,
            F插件编码 = "AutoVoucher",
            F插件名称 = "自动凭证",
            F插件类型 = "Processing",
            F处理粒度 = "batch"
        });
        db.Add(new CfFlowVersion
        {
            FID = 900,
            FFlowDefinitionId = FlowDefinitionId,
            FVersionNumber = 1,
            FIsCurrentVersion = true,
            FStatus = "published"
        });
        db.Add(new CfStageDefinition
        {
            FID = 901,
            FFlowVersionId = 900,
            FStageKey = "auto-voucher",
            FStageName = "自动凭证",
            FType = "auto",
            FSortOrder = 1,
            F插件注册ID = 5,
            F插件规则ID = 902
        });
        db.Add(new CfPluginRule
        {
            FID = 902,
            FOrgId = OrgId,
            F类型编码 = "AutoVoucher",
            F规则名称 = "测试规则",
            F规则配置JSON = JsonSerializer.Serialize(config)
        });
        await db.SaveChangesAsync();
    }

    private static VoucherExplainSourceRow Row(long id, Dictionary<string, object?> fields) => new()
    {
        SourceRowId = id,
        Fields = fields
    };

    private static Dictionary<string, object?> FreightRow(string orderNo = "D001", object? amount = null) => new()
    {
        ["F费用类别"] = "快递运输",
        ["F交易摘要"] = "六月运费",
        ["F金额"] = amount ?? "12.50",
        ["F单号"] = orderNo,
        ["F网点名称"] = "栾城网点",
        ["F业务日期"] = "2026-06-01"
    };

    [Fact]
    public async Task Explain_unmatched_row_reports_no_rule_group()
    {
        await using var db = TestDbContextFactory.Create(nameof(Explain_unmatched_row_reports_no_rule_group));
        await SeedFlowWithConfigAsync(db, CreateConfig());

        var service = new VoucherExplainService(db, NullLogger<VoucherExplainService>.Instance);
        var result = await service.ExplainAsync(CreateContext(), [
            Row(1, new Dictionary<string, object?>
            {
                ["F费用类别"] = "完全无关",
                ["F交易摘要"] = "也无关",
                ["F金额"] = "10"
            })
        ]);

        Assert.True(result.HasConfig);
        var snapshot = result.Rows[1];
        Assert.True(snapshot.PassedFilter);
        Assert.False(snapshot.Matched);
        Assert.Contains(snapshot.Issues, issue => issue.Contains("未命中"));
    }

    [Fact]
    public async Task Explain_routed_but_no_output_when_condition_lines_reject_row()
    {
        await using var db = TestDbContextFactory.Create(nameof(Explain_routed_but_no_output_when_condition_lines_reject_row));
        await SeedFlowWithConfigAsync(db, CreateConfig());

        var service = new VoucherExplainService(db, NullLogger<VoucherExplainService>.Instance);
        var result = await service.ExplainAsync(CreateContext(), [
            Row(1, new Dictionary<string, object?>
            {
                ["F费用类别"] = "条件费用",
                ["F金额"] = "10",
                ["F费用类型"] = "顺丰" // 不在 ConditionValues [EMS] 中，且组内无兜底行
            })
        ]);

        var snapshot = result.Rows[1];
        Assert.True(snapshot.RoutedButNoOutput);
        Assert.False(snapshot.Matched);
        Assert.Equal("条件组", snapshot.RuleGroupName);
        Assert.Contains(snapshot.Issues, issue => issue.Contains("无可接纳"));
    }

    [Fact]
    public async Task Explain_matched_row_builds_draft_with_rule_lines_and_match_reason()
    {
        await using var db = TestDbContextFactory.Create(nameof(Explain_matched_row_builds_draft_with_rule_lines_and_match_reason));
        await SeedFlowWithConfigAsync(db, CreateConfig());
        db.Add(new FinAccount { FID = 1001, FCode = "6601", FName = "销售费用" });
        db.Add(new FinAccount { FID = 2001, FCode = "1001", FName = "库存现金" });
        await db.SaveChangesAsync();

        var service = new VoucherExplainService(db, NullLogger<VoucherExplainService>.Instance);
        var result = await service.ExplainAsync(CreateContext(), [Row(1, FreightRow())]);

        var snapshot = result.Rows[1];
        Assert.True(snapshot.Matched);
        Assert.Equal(2, snapshot.MatchedLayer);
        Assert.Contains("快递", snapshot.MatchReason);
        Assert.Equal("运输费", snapshot.RuleGroupName);

        // 规则配置快照（人工核验"规则怎么配的"）
        Assert.Equal(2, snapshot.RuleLines.Count);
        Assert.Contains(snapshot.RuleLines, line => line.AccountText.Contains("6601"));

        // 参与字段原始值
        Assert.Equal("快递运输", snapshot.SourceFieldValues["F费用类别"]?.ToString());
        Assert.Equal("12.50", snapshot.SourceFieldValues["F金额"]?.ToString());

        // 草案分录：借贷各 12.50，平衡，科目名称已填充
        Assert.Equal(2, snapshot.DraftEntries.Count);
        Assert.Equal(12.50m, snapshot.DraftDebitTotal);
        Assert.Equal(12.50m, snapshot.DraftCreditTotal);
        Assert.True(snapshot.DraftBalanced);
        Assert.Equal("销售费用", snapshot.DraftEntries[0].AccountName);
        Assert.Equal("运费-栾城网点", snapshot.DraftEntries[0].Summary);
        Assert.Equal($"{BatchId}|F单号=D001", snapshot.BusinessKey);
    }

    [Fact]
    public async Task Explain_reports_persistence_finding_when_actual_voucher_missing()
    {
        await using var db = TestDbContextFactory.Create(nameof(Explain_reports_persistence_finding_when_actual_voucher_missing));
        await SeedFlowWithConfigAsync(db, CreateConfig());

        var service = new VoucherExplainService(db, NullLogger<VoucherExplainService>.Instance);
        var result = await service.ExplainAsync(CreateContext(batchStatus: 5), [Row(1, FreightRow())]);

        Assert.Null(result.Rows[1].ActualVoucherId);
        var finding = Assert.Single(result.Findings, f => f.Title == "凭证草案正常但实际凭证缺失");
        Assert.Equal(Dtos.ValidationAttribution.Persistence, finding.Attribution);
    }

    [Fact]
    public async Task Explain_skips_persistence_finding_while_batch_running()
    {
        await using var db = TestDbContextFactory.Create(nameof(Explain_skips_persistence_finding_while_batch_running));
        await SeedFlowWithConfigAsync(db, CreateConfig());

        var service = new VoucherExplainService(db, NullLogger<VoucherExplainService>.Instance);
        var result = await service.ExplainAsync(CreateContext(batchStatus: 4), [Row(1, FreightRow())]);

        Assert.DoesNotContain(result.Findings, f => f.Title == "凭证草案正常但实际凭证缺失");
    }

    [Fact]
    public async Task Explain_reconciles_actual_voucher_by_business_key()
    {
        await using var db = TestDbContextFactory.Create(nameof(Explain_reconciles_actual_voucher_by_business_key));
        await SeedFlowWithConfigAsync(db, CreateConfig());

        var voucher = new FinVoucher
        {
            FID = 7001,
            FVoucherWord = "记",
            FVoucherNo = 18,
            FDate = new DateTime(2026, 6, 1),
            FPeriodId = 1,
            FAccountSetId = 1,
            FOrgId = OrgId,
            FDataScopeId = $"{BatchId}|F单号=D001",
            FCreator = "test"
        };
        voucher.Entries.Add(new FinVoucherEntry
        {
            FID = 7101, FVoucherId = 7001, FLineNo = 1, FSummary = "运费-栾城网点",
            FAccountId = 1001, FAccountCode = "6601", FAccountName = "销售费用",
            FDebitAmount = 12.50m, FCreditAmount = 0, FOrgId = OrgId
        });
        voucher.Entries.Add(new FinVoucherEntry
        {
            FID = 7102, FVoucherId = 7001, FLineNo = 2, FSummary = "运费",
            FAccountId = 2001, FAccountCode = "1001", FAccountName = "库存现金",
            FDebitAmount = 0, FCreditAmount = 12.50m, FOrgId = OrgId
        });
        db.Add(voucher);
        await db.SaveChangesAsync();

        var service = new VoucherExplainService(db, NullLogger<VoucherExplainService>.Instance);
        var result = await service.ExplainAsync(CreateContext(), [Row(1, FreightRow())]);

        var snapshot = result.Rows[1];
        Assert.Equal(7001, snapshot.ActualVoucherId);
        Assert.Equal("记-18", snapshot.ActualVoucherNo);
        Assert.Equal(2, snapshot.ActualEntries.Count);
        Assert.Equal(12.50m, snapshot.ActualDebitTotal);
        Assert.True(snapshot.ActualBalanced);
        // 草案与实际一致：不应产生不一致 finding
        Assert.DoesNotContain(result.Findings, f => f.Title == "凭证解释草案与实际凭证不一致");
        Assert.DoesNotContain(result.Findings, f => f.Title == "凭证草案正常但实际凭证缺失");
    }

    [Fact]
    public async Task Explain_reports_import_data_finding_for_non_numeric_amount()
    {
        await using var db = TestDbContextFactory.Create(nameof(Explain_reports_import_data_finding_for_non_numeric_amount));
        await SeedFlowWithConfigAsync(db, CreateConfig());

        var service = new VoucherExplainService(db, NullLogger<VoucherExplainService>.Instance);
        var result = await service.ExplainAsync(CreateContext(), [Row(1, FreightRow(amount: "十二块五"))]);

        var snapshot = result.Rows[1];
        Assert.Contains(snapshot.DraftEntries, e => e.Issue != null && e.Issue.Contains("不是数字"));
        var finding = Assert.Single(result.Findings, f => f.Title == "凭证金额字段无法解析");
        Assert.Equal(Dtos.ValidationAttribution.ImportData, finding.Attribution);
    }

    [Fact]
    public async Task Explain_reports_configuration_finding_for_unbalanced_draft()
    {
        await using var db = TestDbContextFactory.Create(nameof(Explain_reports_configuration_finding_for_unbalanced_draft));
        // 贷方金额字段取不存在的列 → 贷方分录金额为空 → 草案借贷不平
        await SeedFlowWithConfigAsync(db, CreateConfig(creditAmountField: "F不存在的列"));

        var service = new VoucherExplainService(db, NullLogger<VoucherExplainService>.Instance);
        var result = await service.ExplainAsync(CreateContext(), [Row(1, FreightRow())]);

        var snapshot = result.Rows[1];
        Assert.False(snapshot.DraftBalanced);
        Assert.Contains(result.Findings, f => f.Title == "凭证草案借贷不平");
    }

    [Fact]
    public async Task Explain_returns_empty_when_flow_has_no_voucher_node()
    {
        await using var db = TestDbContextFactory.Create(nameof(Explain_returns_empty_when_flow_has_no_voucher_node));

        var service = new VoucherExplainService(db, NullLogger<VoucherExplainService>.Instance);
        var result = await service.ExplainAsync(CreateContext(), [Row(1, FreightRow())]);

        Assert.False(result.HasConfig);
        Assert.Empty(result.Rows);
        Assert.Empty(result.Findings);
    }
}
