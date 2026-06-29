using Dapper;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Validation;

public class CostValidationAnalyzer
{
    private readonly STOTOPDbContext _db;
    private readonly ValidationAttributionClassifier _classifier;

    public CostValidationAnalyzer(STOTOPDbContext db, ValidationAttributionClassifier classifier)
    {
        _db = db;
        _classifier = classifier;
    }

    public async Task<int> CountAsync(ValidationBatchContext context, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM [EXP出港运单_计费结果_成本明细] c
            INNER JOIN [EXP出港运单_计费结果] r ON c.[F计费结果ID] = r.[FID]
            WHERE r.[F批次ID] = @BatchId
              AND (@OrgId <= 0 OR r.[F组织ID] = @OrgId)
            """;

        try
        {
            return await _db.Database.GetDbConnection()
                .QuerySingleAsync<int>(new CommandDefinition(
                    sql,
                    new { context.BatchId, context.OrgId },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            return 0;
        }
    }

    public async Task<IReadOnlyList<ImportValidationFindingDto>> AnalyzeAsync(
        ValidationBatchContext context,
        ImportValidationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var rows = BuildRepresentativeRows(
            await QueryRowsAsync(context, request, cancellationToken),
            context);
        var findings = new List<ImportValidationFindingDto>();

        foreach (var row in rows)
        {
            var diff = Math.Abs(row.TotalCost - row.BreakdownTotal);
            if (row.BreakdownCount == 0 && row.TotalCost != 0)
            {
                var result = _classifier.Classify(new ValidationClassificationInput
                {
                    ExpectedValue = row.TotalCost,
                    PersistedResultExists = false
                });

                findings.Add(new ImportValidationFindingDto
                {
                    Domain = ValidationDomain.Cost,
                    Attribution = result.Attribution,
                    Severity = result.Severity,
                    Confidence = result.Confidence,
                    AffectedRows = Math.Max(1, row.AffectedRows),
                    SourceRowId = row.BillingResultId,
                    WaybillNo = row.WaybillNo,
                    BusinessKey = row.WaybillNo,
                    Title = "成本明细缺失",
                    Message = "计费结果已有成本合计，但未找到对应成本明细。",
                    ExpectedValue = row.TotalCost,
                    SystemValue = 0,
                    Difference = row.TotalCost,
                    SuggestedAction = result.SuggestedAction,
                    Evidence = BuildEvidence(row, request)
                });
                continue;
            }

            if (diff > context.Tolerance)
            {
                var result = _classifier.Classify(new ValidationClassificationInput
                {
                    ExpectedValue = row.BreakdownTotal,
                    SystemValue = row.TotalCost,
                    Tolerance = context.Tolerance,
                    PersistedResultExists = true
                });

                findings.Add(new ImportValidationFindingDto
                {
                    Domain = ValidationDomain.Cost,
                    Attribution = result.Attribution,
                    Severity = result.Severity,
                    Confidence = result.Confidence,
                    AffectedRows = Math.Max(1, row.AffectedRows),
                    SourceRowId = row.BillingResultId,
                    WaybillNo = row.WaybillNo,
                    BusinessKey = row.WaybillNo,
                    Title = "成本合计与明细不一致",
                    Message = $"计费结果成本合计 {row.TotalCost} 与成本明细合计 {row.BreakdownTotal} 不一致。",
                    ExpectedValue = row.BreakdownTotal,
                    SystemValue = row.TotalCost,
                    Difference = row.TotalCost - row.BreakdownTotal,
                    SuggestedAction = result.SuggestedAction,
                    Evidence = BuildEvidence(row, request)
                });
            }
        }

        return findings;
    }

    private async Task<List<CostValidationRow>> QueryRowsAsync(
        ValidationBatchContext context,
        ImportValidationRunRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH CostRows AS (
                SELECT r.[FID] AS [BillingResultId],
                       r.[F运单编号] AS [WaybillNo],
                       r.[F成本合计] AS [TotalCost],
                       ISNULL(SUM(c.[F金额]), 0) AS [BreakdownTotal],
                       COUNT(c.[FID]) AS [BreakdownCount]
                FROM [EXP出港运单_计费结果] r
                LEFT JOIN [EXP出港运单_计费结果_成本明细] c ON c.[F计费结果ID] = r.[FID]
                WHERE r.[F批次ID] = @BatchId
                  AND (@OrgId <= 0 OR r.[F组织ID] = @OrgId)
                GROUP BY r.[FID], r.[F运单编号], r.[F成本合计]
            ),
            IssueRows AS (
                SELECT *,
                       CASE
                           WHEN [BreakdownCount] = 0 AND [TotalCost] <> 0 THEN N'成本明细缺失'
                           WHEN ABS([TotalCost] - [BreakdownTotal]) > @Tolerance THEN N'成本合计与明细不一致'
                           ELSE NULL
                       END AS [IssueTitle]
                FROM CostRows
            ),
            RankedRows AS (
                SELECT *,
                       COUNT(1) OVER (PARTITION BY [IssueTitle]) AS [AffectedRows],
                       ROW_NUMBER() OVER (PARTITION BY [IssueTitle] ORDER BY [BillingResultId]) AS [RowNumber]
                FROM IssueRows
                WHERE [IssueTitle] IS NOT NULL
            )
            SELECT TOP (@Limit)
                   [BillingResultId],
                   [WaybillNo],
                   [TotalCost],
                   [BreakdownTotal],
                   [BreakdownCount],
                   [AffectedRows]
            FROM RankedRows
            WHERE [RowNumber] = 1
            ORDER BY [BillingResultId]
            """;

        try
        {
            var rows = await _db.Database.GetDbConnection()
                .QueryAsync<CostValidationRow>(new CommandDefinition(
                    sql,
                    new { context.BatchId, context.OrgId, Limit = GetLimit(request), context.Tolerance },
                    cancellationToken: cancellationToken));

            return rows.ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<CostValidationRow> BuildRepresentativeRows(
        IEnumerable<CostValidationRow> rows,
        ValidationBatchContext context)
    {
        return rows
            .GroupBy(row => BuildCostIssueSignature(row, context), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group => group
                .OrderBy(row => row.BillingResultId)
                .First())
            .ToList();
    }

    private static string BuildCostIssueSignature(CostValidationRow row, ValidationBatchContext context)
    {
        var diff = Math.Abs(row.TotalCost - row.BreakdownTotal);
        if (row.BreakdownCount == 0 && row.TotalCost != 0)
            return "成本明细缺失";

        return diff > context.Tolerance
            ? "成本合计与明细不一致"
            : string.Empty;
    }

    private static ImportValidationEvidenceDto BuildEvidence(CostValidationRow row, ImportValidationRunRequest request)
    {
        var evidence = new ImportValidationEvidenceDto
        {
            PersistedResult =
            {
                ["billingResultId"] = row.BillingResultId,
                ["totalCost"] = row.TotalCost,
                ["breakdownTotal"] = row.BreakdownTotal,
                ["breakdownCount"] = row.BreakdownCount
            }
        };

        if (request.IncludeEvidence)
        {
            evidence.TraceSteps.Add(new CalculationTraceStepDto
            {
                Step = "cost-breakdown-sum",
                Description = "汇总计费结果下的成本明细金额，与计费结果成本合计对比。",
                InputValue = row.BreakdownTotal,
                OutputValue = row.TotalCost,
                Formula = "SUM(成本明细.F金额) == 计费结果.F成本合计"
            });
        }

        return evidence;
    }

    private static int GetLimit(ImportValidationRunRequest request)
    {
        if (string.Equals(request.Mode, "all", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Mode, "allLimited", StringComparison.OrdinalIgnoreCase))
            return 5000;

        return Math.Clamp(request.SampleSize, 1, 5000);
    }

    private sealed class CostValidationRow
    {
        public long BillingResultId { get; init; }
        public string? WaybillNo { get; init; }
        public decimal TotalCost { get; init; }
        public decimal BreakdownTotal { get; init; }
        public int BreakdownCount { get; init; }
        public int AffectedRows { get; init; }
    }
}
