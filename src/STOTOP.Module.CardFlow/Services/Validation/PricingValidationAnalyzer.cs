using Dapper;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;

namespace STOTOP.Module.CardFlow.Services.Validation;

public class PricingValidationAnalyzer
{
    private readonly STOTOPDbContext _db;
    private readonly ValidationAttributionClassifier _classifier;

    public PricingValidationAnalyzer(STOTOPDbContext db, ValidationAttributionClassifier classifier)
    {
        _db = db;
        _classifier = classifier;
    }

    public async Task<int> CountAsync(ValidationBatchContext context, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM [EXP出港运单_计费结果]
            WHERE [F批次ID] = @BatchId
              AND (@OrgId <= 0 OR [F组织ID] = @OrgId)
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
        var rows = await QueryRepresentativeErrorRowsAsync(context, request, cancellationToken);
        if (rows.Count == 0 && context.TotalRows > 0)
        {
            var resultCount = await CountAsync(context, cancellationToken);
            if (resultCount > 0)
                return [];

            var result = _classifier.Classify(new ValidationClassificationInput
            {
                ExpectedValue = context.TotalRows,
                PersistedResultExists = false
            });

            return
            [
                new ImportValidationFindingDto
                {
                    Domain = ValidationDomain.Pricing,
                    Attribution = result.Attribution,
                    Severity = result.Severity,
                    Confidence = result.Confidence,
                    AffectedRows = context.TotalRows,
                    Title = "未找到价格计算结果",
                    Message = "该批次已有导入行，但未找到出港运单计费结果。",
                    ExpectedValue = context.TotalRows,
                    SystemValue = 0,
                    Difference = context.TotalRows,
                    SuggestedAction = result.SuggestedAction,
                    Evidence = new ImportValidationEvidenceDto
                    {
                        PersistedResult =
                        {
                            ["batchId"] = context.BatchId
                        }
                    }
                }
            ];
        }

        var findings = new List<ImportValidationFindingDto>();
        foreach (var row in rows)
        {
            var result = ClassifyPriceError(row.ErrorMessage, row.ChargeAmount);
            findings.Add(new ImportValidationFindingDto
            {
                Domain = ValidationDomain.Pricing,
                Attribution = result.Attribution,
                Severity = result.Severity,
                Confidence = result.Confidence,
                AffectedRows = Math.Max(1, row.AffectedRows),
                SourceRowId = row.Id,
                WaybillNo = row.WaybillNo,
                BusinessKey = row.WaybillNo,
                Title = "价格计算异常",
                Message = string.IsNullOrWhiteSpace(row.ErrorMessage)
                    ? "计费结果状态异常。"
                    : NormalizeErrorMessage(row.ErrorMessage),
                SystemValue = row.ChargeAmount,
                SuggestedAction = result.SuggestedAction,
                Evidence = new ImportValidationEvidenceDto
                {
                    PersistedResult =
                    {
                        ["billingResultId"] = row.Id,
                        ["calcStatus"] = row.CalcStatus,
                        ["chargeAmount"] = row.ChargeAmount,
                        ["shopName"] = row.ShopName
                    }
                }
            });
        }

        return findings;
    }

    private async Task<List<PricingValidationRow>> QueryRepresentativeErrorRowsAsync(
        ValidationBatchContext context,
        ImportValidationRunRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(context.TargetTable))
        {
            var rows = await QueryRepresentativeErrorRowsWithSourceAsync(context, request, cancellationToken);
            if (rows != null)
                return rows;
        }

        return await QueryRepresentativeErrorRowsWithoutSourceAsync(context, request, cancellationToken);
    }

    private async Task<List<PricingValidationRow>?> QueryRepresentativeErrorRowsWithSourceAsync(
        ValidationBatchContext context,
        ImportValidationRunRequest request,
        CancellationToken cancellationToken)
    {
        var tableName = QuoteSqlIdentifierPath(context.TargetTable!);
        var shopPartitionSql = BuildShopScopedErrorPartitionSql(
            "r.[F异常信息]",
            "s.[F店铺账号]");
        var sql = $"""
            WITH ErrorRows AS (
                SELECT r.[FID] AS [Id],
                       r.[F运单编号] AS [WaybillNo],
                       s.[F店铺账号] AS [ShopName],
                       r.[F计算状态] AS [CalcStatus],
                       r.[F异常信息] AS [ErrorMessage],
                       r.[F应收金额] AS [ChargeAmount],
                       COUNT(1) OVER (
                           PARTITION BY r.[F计算状态], NULLIF(LTRIM(RTRIM(ISNULL(r.[F异常信息], N''))), N''), {shopPartitionSql}
                       ) AS [AffectedRows],
                       ROW_NUMBER() OVER (
                           PARTITION BY r.[F计算状态], NULLIF(LTRIM(RTRIM(ISNULL(r.[F异常信息], N''))), N''), {shopPartitionSql}
                           ORDER BY r.[FID]
                       ) AS [RowNumber]
                FROM [EXP出港运单_计费结果] r
                LEFT JOIN {tableName} s ON s.[F批次ID] = r.[F批次ID] AND s.[F运单编号] = r.[F运单编号]
                WHERE r.[F批次ID] = @BatchId
                  AND (@OrgId <= 0 OR r.[F组织ID] = @OrgId)
                  AND (r.[F计算状态] <> 1 OR NULLIF(LTRIM(RTRIM(ISNULL(r.[F异常信息], N''))), N'') IS NOT NULL)
            )
            SELECT TOP (@Limit)
                   [Id],
                   [WaybillNo],
                   [ShopName],
                   [CalcStatus],
                   [ErrorMessage],
                   [ChargeAmount],
                   [AffectedRows]
            FROM ErrorRows
            WHERE [RowNumber] = 1
            ORDER BY [Id]
            """;

        try
        {
            var rows = await _db.Database.GetDbConnection()
                .QueryAsync<PricingValidationRow>(new CommandDefinition(
                    sql,
                    new { context.BatchId, context.OrgId, Limit = GetLimit(request) },
                    cancellationToken: cancellationToken));

            return rows.ToList();
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<PricingValidationRow>> QueryRepresentativeErrorRowsWithoutSourceAsync(
        ValidationBatchContext context,
        ImportValidationRunRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH ErrorRows AS (
                SELECT [FID] AS [Id],
                       [F运单编号] AS [WaybillNo],
                       [F计算状态] AS [CalcStatus],
                       [F异常信息] AS [ErrorMessage],
                       [F应收金额] AS [ChargeAmount],
                       COUNT(1) OVER (
                           PARTITION BY [F计算状态], NULLIF(LTRIM(RTRIM(ISNULL([F异常信息], N''))), N'')
                       ) AS [AffectedRows],
                       ROW_NUMBER() OVER (
                           PARTITION BY [F计算状态], NULLIF(LTRIM(RTRIM(ISNULL([F异常信息], N''))), N'')
                           ORDER BY [FID]
                       ) AS [RowNumber]
                FROM [EXP出港运单_计费结果]
                WHERE [F批次ID] = @BatchId
                  AND (@OrgId <= 0 OR [F组织ID] = @OrgId)
                  AND ([F计算状态] <> 1 OR NULLIF(LTRIM(RTRIM(ISNULL([F异常信息], N''))), N'') IS NOT NULL)
            )
            SELECT TOP (@Limit)
                   [Id],
                   [WaybillNo],
                   [CalcStatus],
                   [ErrorMessage],
                   [ChargeAmount],
                   [AffectedRows]
            FROM ErrorRows
            WHERE [RowNumber] = 1
            ORDER BY [Id]
            """;

        try
        {
            var rows = await _db.Database.GetDbConnection()
                .QueryAsync<PricingValidationRow>(new CommandDefinition(
                    sql,
                    new { context.BatchId, context.OrgId, Limit = GetLimit(request) },
                    cancellationToken: cancellationToken));

            return rows.ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string BuildShopScopedErrorPartitionSql(string errorColumnSql, string shopColumnSql)
    {
        return $"""
            CASE
                WHEN {errorColumnSql} LIKE N'ERR_SHOP%'
                  OR {errorColumnSql} LIKE N'ERR_NO_PRICE_PLAN:%'
                  OR {errorColumnSql} LIKE N'ERR_NO_PRICE_CELL:%'
                  OR {errorColumnSql} LIKE N'%店铺%'
                THEN NULLIF(LTRIM(RTRIM(ISNULL({shopColumnSql}, N''))), N'')
                ELSE NULL
            END
            """;
    }

    private ValidationClassificationResult ClassifyPriceError(string? errorMessage, decimal? chargeAmount)
    {
        var message = errorMessage ?? string.Empty;
        var missingFields = new List<string>();
        var configIssues = new List<string>();

        if (message.Contains("重量", StringComparison.OrdinalIgnoreCase)
            || message.Contains("省份", StringComparison.OrdinalIgnoreCase)
            || message.Contains("目的地", StringComparison.OrdinalIgnoreCase)
            || message.Contains("客户", StringComparison.OrdinalIgnoreCase))
        {
            missingFields.Add("计费所需导入字段");
        }

        if (message.Contains("报价", StringComparison.OrdinalIgnoreCase)
            || message.Contains("价格", StringComparison.OrdinalIgnoreCase)
            || message.Contains("网点", StringComparison.OrdinalIgnoreCase)
            || message.Contains("未命中", StringComparison.OrdinalIgnoreCase)
            || message.Contains("规则", StringComparison.OrdinalIgnoreCase))
        {
            configIssues.Add("报价/价格/网点配置未命中");
        }

        return _classifier.Classify(new ValidationClassificationInput
        {
            MissingRequiredFields = missingFields,
            ConfigurationMatched = configIssues.Count == 0,
            ConfigurationIssues = configIssues,
            SystemValue = chargeAmount,
            PersistedResultExists = chargeAmount.HasValue
        });
    }

    private static string NormalizeErrorMessage(string? message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? string.Empty
            : message.Trim();
    }

    private static string QuoteSqlIdentifierPath(string identifier)
    {
        var parts = identifier
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("目标暂存表名无效。");

        return string.Join(".", parts.Select(part => $"[{part.Replace("]", "]]")}]"));
    }

    private static int GetLimit(ImportValidationRunRequest request)
    {
        if (string.Equals(request.Mode, "all", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Mode, "allLimited", StringComparison.OrdinalIgnoreCase))
            return 5000;

        return Math.Clamp(request.SampleSize, 1, 5000);
    }

    private sealed class PricingValidationRow
    {
        public long Id { get; init; }
        public string? WaybillNo { get; init; }
        public string? ShopName { get; init; }
        public int CalcStatus { get; init; }
        public string? ErrorMessage { get; init; }
        public decimal? ChargeAmount { get; init; }
        public int AffectedRows { get; init; }
    }
}
