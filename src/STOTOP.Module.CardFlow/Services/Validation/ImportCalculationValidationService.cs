using Microsoft.EntityFrameworkCore;
using Dapper;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Validation;

public class ImportCalculationValidationService : IImportCalculationValidationService
{
    private readonly STOTOPDbContext _db;
    private readonly VoucherValidationAnalyzer _voucherAnalyzer;
    private readonly PricingValidationAnalyzer _pricingAnalyzer;
    private readonly CostValidationAnalyzer _costAnalyzer;
    private readonly VoucherExplainService? _voucherExplainService;
    private readonly IPricingExplainProvider? _pricingExplainProvider;

    public ImportCalculationValidationService(
        STOTOPDbContext db,
        VoucherValidationAnalyzer voucherAnalyzer,
        PricingValidationAnalyzer pricingAnalyzer,
        CostValidationAnalyzer costAnalyzer,
        VoucherExplainService? voucherExplainService = null,
        IPricingExplainProvider? pricingExplainProvider = null)
    {
        _db = db;
        _voucherAnalyzer = voucherAnalyzer;
        _pricingAnalyzer = pricingAnalyzer;
        _costAnalyzer = costAnalyzer;
        _voucherExplainService = voucherExplainService;
        _pricingExplainProvider = pricingExplainProvider;
    }

    public async Task<ImportValidationSummaryDto> GetSummaryAsync(
        long batchId,
        long orgId,
        CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(batchId, orgId, 0.01m, cancellationToken);
        var flowName = await _db.Set<CfFlowDefinition>()
            .AsNoTracking()
            .Where(flow => flow.FID == context.FlowDefinitionId)
            .Select(flow => flow.FFlowName)
            .FirstOrDefaultAsync(cancellationToken);

        return new ImportValidationSummaryDto
        {
            BatchId = context.BatchId,
            BatchNo = context.BatchNo,
            FlowName = flowName,
            TargetTable = context.TargetTable,
            TotalRows = context.TotalRows,
            BatchStatus = context.BatchStatus,
            BatchStatusText = FormatBatchStatus(context),
            IsBatchRunning = context.IsBatchRunning,
            IsRevoked = context.IsRevoked,
            CurrentNodeName = context.CurrentNodeName,
            ProgressPercent = context.ProgressPercent,
            BatchErrorMessage = context.BatchErrorMessage,
            ImportStartTime = context.ImportStartTime,
            ImportEndTime = context.ImportEndTime,
            ExistingResultCounts =
            {
                [ValidationDomain.Voucher] = await _voucherAnalyzer.CountAsync(context.BatchId, cancellationToken),
                [ValidationDomain.Pricing] = await _pricingAnalyzer.CountAsync(context, cancellationToken),
                [ValidationDomain.Cost] = await _costAnalyzer.CountAsync(context, cancellationToken)
            }
        };
    }

    public async Task<ImportValidationReportDto> RunAsync(
        long batchId,
        long orgId,
        ImportValidationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        request ??= new ImportValidationRunRequest();
        var context = await BuildContextAsync(batchId, orgId, request.Tolerance, cancellationToken);
        var domains = NormalizeDomains(request);
        var findings = new List<ImportValidationFindingDto>();

        if (domains.Contains(ValidationDomain.Voucher))
            findings.AddRange(await _voucherAnalyzer.AnalyzeAsync(context, request, cancellationToken));

        if (domains.Contains(ValidationDomain.Pricing))
            findings.AddRange(await _pricingAnalyzer.AnalyzeAsync(context, request, cancellationToken));

        if (domains.Contains(ValidationDomain.Cost))
            findings.AddRange(await _costAnalyzer.AnalyzeAsync(context, request, cancellationToken));

        var representativeFindings = BuildRepresentativeFindings(findings);
        if (context.IsBatchRunning)
            representativeFindings.Insert(0, BuildBatchRunningFinding(context));

        var (sampleRows, explainFindings) = await BuildSampleRowsAsync(context, request, domains, representativeFindings, cancellationToken);
        if (explainFindings.Count > 0)
            representativeFindings.AddRange(BuildRepresentativeFindings(explainFindings));

        return new ImportValidationReportDto
        {
            BatchId = context.BatchId,
            BatchStatusText = FormatBatchStatus(context),
            IsBatchRunning = context.IsBatchRunning,
            GeneratedAt = DateTime.UtcNow,
            CheckedRows = sampleRows.Count > 0 ? sampleRows.Count : CalculateCheckedRows(context.TotalRows, request),
            SampleRows = sampleRows,
            Findings = representativeFindings
                .OrderByDescending(f => f.Severity)
                .ThenBy(f => f.Domain)
                .ThenBy(f => f.SourceRowId ?? long.MaxValue)
                .ToList(),
            AttributionCounts = representativeFindings
                .GroupBy(f => f.Attribution)
                .ToDictionary(g => g.Key, g => g.Count()),
            SeverityCounts = representativeFindings
                .GroupBy(f => f.Severity)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    public async Task<ImportValidationFindingDto?> GetRowDetailAsync(
        long batchId,
        long rowId,
        long orgId,
        CancellationToken cancellationToken = default)
    {
        var request = new ImportValidationRunRequest
        {
            Mode = "sample",
            SampleSize = 500,
            IncludeEvidence = true
        };
        var report = await RunAsync(batchId, orgId, request, cancellationToken);
        return report.Findings.FirstOrDefault(f => f.SourceRowId == rowId);
    }

    private async Task<ValidationBatchContext> BuildContextAsync(
        long batchId,
        long orgId,
        decimal tolerance,
        CancellationToken cancellationToken)
    {
        var batch = await _db.Set<CfBatch>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId && (orgId <= 0 || b.FOrgId == orgId), cancellationToken);

        if (batch == null)
            throw new InvalidOperationException("批次不存在或无权访问。");

        return new ValidationBatchContext
        {
            BatchId = batch.FID,
            FlowDefinitionId = batch.FFlowDefinitionId,
            OrgId = batch.FOrgId,
            BatchNo = batch.FBatchNo,
            TargetTable = batch.FActualTargetTable,
            TotalRows = batch.FTotalRows,
            Tolerance = tolerance <= 0 ? 0.01m : tolerance,
            BatchStatus = batch.FStatus,
            IsRevoked = batch.FIsRevoked,
            CurrentNodeName = batch.FCurrentNodeName,
            ProgressPercent = batch.FProgressPercent,
            BatchErrorMessage = batch.FErrorMessage,
            ImportStartTime = batch.FImportStartTime,
            ImportEndTime = batch.FImportEndTime
        };
    }

    private static string FormatBatchStatus(ValidationBatchContext context)
    {
        if (context.IsRevoked)
            return "已回撤";

        return context.BatchStatus switch
        {
            0 => "解析中",
            1 => "已暂存",
            2 => "质检中",
            3 => "已创建卡片",
            4 => "处理中",
            5 => "已完成",
            _ => $"未知状态({context.BatchStatus})"
        };
    }

    /// <summary>
    /// 批次节点链未执行完时，计费/成本结果可能尚未写入。
    /// 注入一条阻断级 finding，避免“结果为 0”被误读为计算失败。
    /// </summary>
    private static ImportValidationFindingDto BuildBatchRunningFinding(ValidationBatchContext context)
    {
        var nodeHint = string.IsNullOrWhiteSpace(context.CurrentNodeName)
            ? string.Empty
            : $"当前节点[{context.CurrentNodeName}]";

        return new ImportValidationFindingDto
        {
            Domain = ValidationDomain.Pricing,
            Attribution = ValidationAttribution.Persistence,
            Severity = ValidationSeverity.Blocker,
            Confidence = 1m,
            AffectedRows = context.TotalRows,
            Title = "批次仍在执行中",
            Message = $"批次状态为[{FormatBatchStatus(context)}]{nodeHint}，计费与成本结果可能尚未写入，本次验证结果不完整。请等待批次完成后重新验证。",
            SuggestedAction = "等待批次节点链执行完成后，刷新批次概览并重新执行验证。",
            Evidence = new ImportValidationEvidenceDto
            {
                PersistedResult =
                {
                    ["batchId"] = context.BatchId,
                    ["batchStatus"] = context.BatchStatus,
                    ["currentNodeName"] = context.CurrentNodeName,
                    ["progressPercent"] = context.ProgressPercent
                }
            }
        };
    }

    private static HashSet<ValidationDomain> NormalizeDomains(ImportValidationRunRequest request)
    {
        if (request.Domains.Count == 0)
            return [ValidationDomain.Voucher, ValidationDomain.Pricing, ValidationDomain.Cost];

        return request.Domains.ToHashSet();
    }

    private static bool IsErrorsOnlyMode(ImportValidationRunRequest request)
        => string.Equals(request.Mode, "errorsOnly", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllMode(ImportValidationRunRequest request)
        => string.Equals(request.Mode, "all", StringComparison.OrdinalIgnoreCase)
           || string.Equals(request.Mode, "allLimited", StringComparison.OrdinalIgnoreCase);

    private static int CalculateCheckedRows(int totalRows, ImportValidationRunRequest request)
    {
        if (!IsAllMode(request))
            return Math.Min(totalRows, Math.Max(1, request.SampleSize));

        return totalRows;
    }

    private async Task<(List<ImportValidationSampleRowDto> Rows, List<ImportValidationFindingDto> ExplainFindings)> BuildSampleRowsAsync(
        ValidationBatchContext context,
        ImportValidationRunRequest request,
        HashSet<ValidationDomain> domains,
        IReadOnlyList<ImportValidationFindingDto> findings,
        CancellationToken cancellationToken)
    {
        var limit = GetLimit(request);
        var explainFindings = new List<ImportValidationFindingDto>();
        var sampleWaybillNos = await BuildSampleWaybillNosAsync(
            context,
            request,
            domains,
            findings,
            cancellationToken);

        var sourceRows = sampleWaybillNos.Count > 0
            ? await QuerySourceRowsByWaybillNosAsync(context, sampleWaybillNos, cancellationToken)
            : [];

        // 异常优先模式不回填正常行：没有问题行就返回空抽样，而不是用批次头部行凑数
        if (sourceRows.Count < limit && !IsErrorsOnlyMode(request))
        {
            var fallbackRows = await QuerySourceRowsAsync(context, request, cancellationToken);
            var existingWaybillNos = sourceRows
                .Select(row => GetString(row.Fields, "F运单编号", "F运单号", "F单号", "F关联单号"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            sourceRows.AddRange(fallbackRows
                .Where(row =>
                {
                    var waybillNo = GetString(row.Fields, "F运单编号", "F运单号", "F单号", "F关联单号");
                    return string.IsNullOrWhiteSpace(waybillNo) || existingWaybillNos.Add(waybillNo);
                })
                .Take(limit - sourceRows.Count));
        }

        if (sourceRows.Count == 0)
            return ([], explainFindings);

        if (sampleWaybillNos.Count > 0)
            sourceRows = OrderSourceRowsByWaybillNos(sourceRows, sampleWaybillNos)
                .Take(limit)
                .ToList();

        var sourceWaybillNos = sourceRows
            .Select(row => GetString(row.Fields, "F运单编号", "F运单号", "F单号", "F关联单号"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pricingResults = domains.Contains(ValidationDomain.Pricing)
            ? await QueryPricingSampleResultsAsync(context, sourceWaybillNos, cancellationToken)
            : new Dictionary<string, PricingSampleResult>(StringComparer.OrdinalIgnoreCase);

        var pricingExplains = domains.Contains(ValidationDomain.Pricing) && request.IncludeEvidence
            ? await QueryPricingExplainsAsync(context, sourceWaybillNos, cancellationToken)
            : new Dictionary<string, PricingExplainSnapshot>(StringComparer.OrdinalIgnoreCase);

        var costResults = domains.Contains(ValidationDomain.Cost)
            ? await QueryCostSampleResultsAsync(context, sourceWaybillNos, cancellationToken)
            : new Dictionary<string, CostSampleResult>(StringComparer.OrdinalIgnoreCase);

        var voucherSummary = domains.Contains(ValidationDomain.Voucher)
            ? await QueryVoucherSummaryAsync(context.BatchId, request, cancellationToken)
            : null;

        var voucherExplains = domains.Contains(ValidationDomain.Voucher) && request.IncludeEvidence
            ? await QueryVoucherExplainsAsync(context, sourceRows, cancellationToken)
            : new VoucherExplainBatchResult();
        explainFindings.AddRange(voucherExplains.Findings);

        var rows = sourceRows.Select(row =>
        {
            var waybillNo = GetString(row.Fields, "F运单编号", "F运单号", "F单号", "F关联单号");
            var businessKey = waybillNo
                ?? GetString(row.Fields, "F审批编号", "F数据ID", "F数据id", "F业务摘要")
                ?? row.SourceRowId?.ToString();

            var sample = new ImportValidationSampleRowDto
            {
                SourceRowId = row.SourceRowId,
                WaybillNo = waybillNo,
                BusinessKey = businessKey,
                SourceFields = row.Fields,
                Findings = MatchFindings(findings, row.SourceRowId, waybillNo, businessKey)
            };

            if (domains.Contains(ValidationDomain.Voucher))
                sample.Results.Add(BuildVoucherSampleResult(row.Fields, row.SourceRowId, voucherSummary, voucherExplains));

            if (domains.Contains(ValidationDomain.Pricing))
                sample.Results.Add(BuildPricingSampleResult(row.Fields, waybillNo, pricingResults, pricingExplains, context, explainFindings));

            if (domains.Contains(ValidationDomain.Cost))
                sample.Results.Add(BuildCostSampleResult(row.Fields, waybillNo, costResults));

            return sample;
        }).ToList();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.WaybillNo))
                continue;
            row.Findings.AddRange(explainFindings.Where(f =>
                string.Equals(f.WaybillNo, row.WaybillNo, StringComparison.OrdinalIgnoreCase)));
        }

        return (rows, explainFindings);
    }

    /// <summary>凭证行级解释；服务未注册或解释失败时返回空结果（验证降级为记录级校验）。</summary>
    private async Task<VoucherExplainBatchResult> QueryVoucherExplainsAsync(
        ValidationBatchContext context,
        IReadOnlyList<SourceSampleRow> sourceRows,
        CancellationToken cancellationToken)
    {
        if (_voucherExplainService == null || sourceRows.Count == 0)
            return new VoucherExplainBatchResult();

        var explainRows = sourceRows
            .Where(row => row.SourceRowId.HasValue)
            .Select(row => new VoucherExplainSourceRow
            {
                SourceRowId = row.SourceRowId,
                Fields = row.Fields
            })
            .ToList();

        return await _voucherExplainService.ExplainAsync(context, explainRows, cancellationToken);
    }

    /// <summary>调用 Express 注册的价格解释提供方；未注册或解释失败时返回空（验证降级为仅落库校验）。</summary>
    private async Task<IReadOnlyDictionary<string, PricingExplainSnapshot>> QueryPricingExplainsAsync(
        ValidationBatchContext context,
        IReadOnlyCollection<string> sourceWaybillNos,
        CancellationToken cancellationToken)
    {
        if (_pricingExplainProvider == null || sourceWaybillNos.Count == 0)
            return new Dictionary<string, PricingExplainSnapshot>(StringComparer.OrdinalIgnoreCase);

        try
        {
            return await _pricingExplainProvider.ExplainAsync(
                new PricingExplainRequest
                {
                    BatchId = context.BatchId,
                    FlowDefinitionId = context.FlowDefinitionId,
                    OrgId = context.OrgId,
                    WaybillNos = sourceWaybillNos.ToList()
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new Dictionary<string, PricingExplainSnapshot>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<List<string>> BuildSampleWaybillNosAsync(
        ValidationBatchContext context,
        ImportValidationRunRequest request,
        HashSet<ValidationDomain> domains,
        IReadOnlyList<ImportValidationFindingDto> findings,
        CancellationToken cancellationToken)
    {
        var limit = GetLimit(request);
        if (limit <= 0)
            return [];

        if (IsErrorsOnlyMode(request))
            return await BuildErrorsOnlySampleWaybillNosAsync(context, findings, limit, cancellationToken);

        var representativeIssueWaybillNos = findings
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.Domain)
            .Select(finding => finding.WaybillNo)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var issueBudget = representativeIssueWaybillNos.Count == 0
            ? 0
            : Math.Min(representativeIssueWaybillNos.Count, Math.Max(1, limit / 5));
        var calculatedTarget = Math.Max(0, limit - issueBudget);
        var calculatedWaybillNos = await QueryCalculatedSampleWaybillNosAsync(
            context,
            domains,
            calculatedTarget,
            representativeIssueWaybillNos,
            cancellationToken);

        var selectedIssueLimit = calculatedWaybillNos.Count > 0
            ? Math.Min(issueBudget, limit - calculatedWaybillNos.Count)
            : Math.Min(representativeIssueWaybillNos.Count, limit);
        var selectedIssueWaybillNos = representativeIssueWaybillNos
            .Take(Math.Max(0, selectedIssueLimit))
            .ToList();

        return calculatedWaybillNos
            .Concat(selectedIssueWaybillNos)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private async Task<List<SourceSampleRow>> QuerySourceRowsAsync(
        ValidationBatchContext context,
        ImportValidationRunRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.TargetTable))
            return [];

        var tableName = QuoteSqlIdentifierPath(context.TargetTable);
        var sql = $"""
            SELECT TOP (@Limit) *
            FROM {tableName}
            WHERE [F批次ID] = @BatchId
            ORDER BY [FID]
            """;

        try
        {
            var rows = await _db.Database.GetDbConnection()
                .QueryAsync(new CommandDefinition(
                    sql,
                    new { context.BatchId, Limit = GetLimit(request) },
                    cancellationToken: cancellationToken));

            return rows
                .Select(row => ToDictionary(row))
                .Select(fields => new SourceSampleRow
                {
                    SourceRowId = GetLong(fields, "FID", "F原始行号"),
                    Fields = fields
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<SourceSampleRow>> QuerySourceRowsByWaybillNosAsync(
        ValidationBatchContext context,
        IReadOnlyCollection<string> waybillNos,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.TargetTable) || waybillNos.Count == 0)
            return [];

        var tableName = QuoteSqlIdentifierPath(context.TargetTable);
        var rows = new List<SourceSampleRow>();
        foreach (var waybillColumn in SourceWaybillColumns)
        {
            var sql = $"""
                SELECT *
                FROM {tableName}
                WHERE [F批次ID] = @BatchId
                  AND {QuoteSqlIdentifier(waybillColumn)} IN @WaybillNos
                ORDER BY [FID]
                """;

            try
            {
                foreach (var chunk in ChunkWaybillNos(waybillNos))
                {
                    var chunkRows = await _db.Database.GetDbConnection()
                        .QueryAsync(new CommandDefinition(
                            sql,
                            new { context.BatchId, WaybillNos = chunk },
                            cancellationToken: cancellationToken));

                    rows.AddRange(chunkRows
                        .Select(row => ToDictionary(row))
                        .Select(fields => new SourceSampleRow
                        {
                            SourceRowId = GetLong(fields, "FID", "F原始行号"),
                            Fields = fields
                        }));
                }

                if (rows.Count > 0)
                    return rows;
            }
            catch
            {
                rows.Clear();
            }
        }

        return [];
    }

    /// <summary>异常优先模式：抽样全部来自问题行（findings 命中的运单 + 计费失败运单），不足不回填正常行。</summary>
    private async Task<List<string>> BuildErrorsOnlySampleWaybillNosAsync(
        ValidationBatchContext context,
        IReadOnlyList<ImportValidationFindingDto> findings,
        int limit,
        CancellationToken cancellationToken)
    {
        var selected = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var issueWaybillNos = findings
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.Domain)
            .Select(finding => finding.WaybillNo)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!);
        AddDistinctWaybillNos(selected, seen, issueWaybillNos, limit);

        if (selected.Count < limit)
        {
            var errorWaybillNos = await QueryErrorPricingWaybillNosAsync(
                context,
                BuildCandidateQueryLimit(limit - selected.Count, seen.Count),
                cancellationToken);
            AddDistinctWaybillNos(selected, seen, errorWaybillNos, limit);
        }

        return selected;
    }

    private async Task<List<string>> QueryErrorPricingWaybillNosAsync(
        ValidationBatchContext context,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@Limit) [F运单编号] AS [WaybillNo]
            FROM [EXP出港运单_计费结果]
            WHERE [F批次ID] = @BatchId
              AND (@OrgId <= 0 OR [F组织ID] = @OrgId)
              AND [F运单编号] IS NOT NULL
              AND ([F计算状态] <> 1
                   OR NULLIF(LTRIM(RTRIM(ISNULL([F异常信息], N''))), N'') IS NOT NULL)
            ORDER BY [FID]
            """;

        try
        {
            var rows = await _db.Database.GetDbConnection()
                .QueryAsync<string>(new CommandDefinition(
                    sql,
                    new { context.BatchId, context.OrgId, Limit = limit },
                    cancellationToken: cancellationToken));

            return rows
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<string>> QueryCalculatedSampleWaybillNosAsync(
        ValidationBatchContext context,
        HashSet<ValidationDomain> domains,
        int limit,
        IReadOnlyCollection<string> excludedWaybillNos,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
            return [];

        var selected = new List<string>();
        var seen = excludedWaybillNos
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (domains.Contains(ValidationDomain.Cost))
        {
            var costWaybillNos = await QueryCalculatedCostWaybillNosAsync(
                context,
                BuildCandidateQueryLimit(limit, seen.Count),
                cancellationToken);
            AddDistinctWaybillNos(selected, seen, costWaybillNos, limit);
        }

        if (selected.Count < limit && domains.Contains(ValidationDomain.Pricing))
        {
            var pricingWaybillNos = await QueryCalculatedPricingWaybillNosAsync(
                context,
                BuildCandidateQueryLimit(limit - selected.Count, seen.Count),
                cancellationToken);
            AddDistinctWaybillNos(selected, seen, pricingWaybillNos, limit);
        }

        return selected;
    }

    private async Task<List<string>> QueryCalculatedPricingWaybillNosAsync(
        ValidationBatchContext context,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@Limit) [F运单编号] AS [WaybillNo]
            FROM [EXP出港运单_计费结果]
            WHERE [F批次ID] = @BatchId
              AND (@OrgId <= 0 OR [F组织ID] = @OrgId)
              AND [F运单编号] IS NOT NULL
              AND [F计算状态] = 1
              AND NULLIF(LTRIM(RTRIM(ISNULL([F异常信息], N''))), N'') IS NULL
              AND [F应收金额] IS NOT NULL
            ORDER BY [FID]
            """;

        try
        {
            var rows = await _db.Database.GetDbConnection()
                .QueryAsync<string>(new CommandDefinition(
                    sql,
                    new { context.BatchId, context.OrgId, Limit = limit },
                    cancellationToken: cancellationToken));

            return rows
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<string>> QueryCalculatedCostWaybillNosAsync(
        ValidationBatchContext context,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@Limit) r.[F运单编号] AS [WaybillNo]
            FROM [EXP出港运单_计费结果] r
            INNER JOIN [EXP出港运单_计费结果_成本明细] c ON c.[F计费结果ID] = r.[FID]
            WHERE r.[F批次ID] = @BatchId
              AND (@OrgId <= 0 OR r.[F组织ID] = @OrgId)
              AND r.[F运单编号] IS NOT NULL
            GROUP BY r.[FID], r.[F运单编号]
            ORDER BY r.[FID]
            """;

        try
        {
            var rows = await _db.Database.GetDbConnection()
                .QueryAsync<string>(new CommandDefinition(
                    sql,
                    new { context.BatchId, context.OrgId, Limit = limit },
                    cancellationToken: cancellationToken));

            return rows
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<Dictionary<string, PricingSampleResult>> QueryPricingSampleResultsAsync(
        ValidationBatchContext context,
        IReadOnlyCollection<string> sourceWaybillNos,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT [FID] AS [Id],
                   [F运单编号] AS [WaybillNo],
                   [F计算状态] AS [CalcStatus],
                   [F异常信息] AS [ErrorMessage],
                   [F应收金额] AS [ChargeAmount],
                   [F计费重量] AS [BillingWeight],
                   [F报价编号] AS [QuotationCode],
                   [F业务对象类型] AS [ClientType]
            FROM [EXP出港运单_计费结果]
            WHERE [F批次ID] = @BatchId
              AND (@OrgId <= 0 OR [F组织ID] = @OrgId)
              AND [F运单编号] IN @WaybillNos
            ORDER BY [FID]
            """;

        if (sourceWaybillNos.Count == 0)
            return new Dictionary<string, PricingSampleResult>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var rows = new List<PricingSampleResult>();
            foreach (var waybillNos in ChunkWaybillNos(sourceWaybillNos))
            {
                var chunkRows = await _db.Database.GetDbConnection()
                    .QueryAsync<PricingSampleResult>(new CommandDefinition(
                        sql,
                        new { context.BatchId, context.OrgId, WaybillNos = waybillNos },
                        cancellationToken: cancellationToken));
                rows.AddRange(chunkRows);
            }

            return rows
                .Where(row => !string.IsNullOrWhiteSpace(row.WaybillNo))
                .GroupBy(row => row.WaybillNo!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, PricingSampleResult>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<Dictionary<string, CostSampleResult>> QueryCostSampleResultsAsync(
        ValidationBatchContext context,
        IReadOnlyCollection<string> sourceWaybillNos,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH BillingRows AS (
                SELECT [FID] AS [BillingResultId],
                       [F运单编号] AS [WaybillNo],
                       [F成本合计] AS [TotalCost]
                FROM [EXP出港运单_计费结果]
                WHERE [F批次ID] = @BatchId
                  AND (@OrgId <= 0 OR [F组织ID] = @OrgId)
                  AND [F运单编号] IN @WaybillNos
            )
            SELECT b.[BillingResultId],
                   b.[WaybillNo],
                   b.[TotalCost],
                   c.[FID] AS [CostBreakdownId],
                   c.[F成本项目ID] AS [CostItemId],
                   -- F成本项目ID 存的是方案成本项ID（矩阵JSON由前端按方案项ID写入），
                   -- 必须方案项名优先；全局成本项目表仅作旧数据兜底，ID 撞号时全局名会张冠李戴
                   COALESCE(i.[F成本项名称], base.[F名称]) AS [CostItemName],
                   c.[F金额] AS [CostAmount]
            FROM BillingRows b
            LEFT JOIN [EXP出港运单_计费结果_成本明细] c ON c.[F计费结果ID] = b.[BillingResultId]
            LEFT JOIN [EXP成本方案_成本项] i ON i.[FID] = c.[F成本项目ID]
            LEFT JOIN [EXP成本项目] base ON base.[FID] = c.[F成本项目ID]
            ORDER BY b.[BillingResultId], base.[F排序], i.[F排序号], c.[FID]
            """;

        if (sourceWaybillNos.Count == 0)
            return new Dictionary<string, CostSampleResult>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var lines = new List<CostSampleLine>();
            foreach (var waybillNos in ChunkWaybillNos(sourceWaybillNos))
            {
                var chunkLines = await _db.Database.GetDbConnection()
                    .QueryAsync<CostSampleLine>(new CommandDefinition(
                        sql,
                        new { context.BatchId, context.OrgId, WaybillNos = waybillNos },
                        cancellationToken: cancellationToken));
                lines.AddRange(chunkLines);
            }

            return lines
                .GroupBy(line => line.BillingResultId)
                .Select(group =>
                {
                    var first = group.First();
                    var items = group
                        .Where(line => line.CostBreakdownId.HasValue)
                        .Select(line => new CostItemSampleResult
                        {
                            CostItemId = line.CostItemId ?? 0,
                            CostItemName = string.IsNullOrWhiteSpace(line.CostItemName)
                                ? $"成本项 {line.CostItemId}"
                                : line.CostItemName!,
                            Amount = line.CostAmount ?? 0m
                        })
                        .ToList();

                    return new CostSampleResult
                    {
                        BillingResultId = first.BillingResultId,
                        WaybillNo = first.WaybillNo,
                        TotalCost = first.TotalCost,
                        BreakdownTotal = items.Sum(item => item.Amount),
                        BreakdownCount = items.Count,
                        CostItems = items
                    };
                })
                .Where(row => !string.IsNullOrWhiteSpace(row.WaybillNo))
                .GroupBy(row => row.WaybillNo!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, CostSampleResult>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<VoucherSampleSummary?> QueryVoucherSummaryAsync(
        long batchId,
        ImportValidationRunRequest request,
        CancellationToken cancellationToken)
    {
        var records = await _db.Set<CfVoucherRecord>()
            .AsNoTracking()
            .Where(record => record.FBatchId == batchId)
            .OrderBy(record => record.FID)
            .Take(GetLimit(request))
            .ToListAsync(cancellationToken);

        if (records.Count == 0)
            return null;

        return new VoucherSampleSummary
        {
            RecordCount = records.Count,
            TotalRows = records.Sum(record => record.FTotalRows),
            MatchedRows = records.Sum(record => record.FMatchedRows),
            UnmatchedRows = records.Sum(record => record.FUnmatchedRows),
            GeneratedVoucherCount = records.Sum(record => record.FGeneratedVoucherCount),
            HasError = records.Any(record => record.FStatus == 3 || !string.IsNullOrWhiteSpace(record.FErrorMessage)),
            FirstErrorMessage = records.Select(record => record.FErrorMessage).FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
        };
    }

    private static ImportValidationSampleResultDto BuildVoucherSampleResult(
        IReadOnlyDictionary<string, object?> sourceFields,
        long? sourceRowId,
        VoucherSampleSummary? summary,
        VoucherExplainBatchResult voucherExplains)
    {
        // 行级解释可用时输出三段式核验明细（原始字段→规则配置→凭证结果）
        if (sourceRowId.HasValue && voucherExplains.Rows.TryGetValue(sourceRowId.Value, out var explain))
            return BuildVoucherRowResult(explain);

        var sourceStatus = GetValue(sourceFields, "F凭证生成状态", "F审批状态", "F审批结果");
        if (summary == null)
        {
            return new ImportValidationSampleResultDto
            {
                Domain = ValidationDomain.Voucher,
                Label = "自动凭证",
                OriginalValue = sourceStatus,
                Status = "missing",
                Message = "未找到该批次的凭证生成记录。"
            };
        }

        var status = summary.HasError
            ? "error"
            : summary.UnmatchedRows > 0 ? "different" : "ok";

        return new ImportValidationSampleResultDto
        {
            Domain = ValidationDomain.Voucher,
            Label = "自动凭证",
            OriginalValue = sourceStatus,
            SystemValue = $"{summary.GeneratedVoucherCount} 张凭证",
            ExpectedValue = $"{summary.MatchedRows}/{summary.TotalRows} 行已匹配",
            Difference = summary.UnmatchedRows,
            Status = status,
            Message = summary.HasError
                ? summary.FirstErrorMessage ?? "凭证生成存在异常。"
                : summary.UnmatchedRows > 0
                    ? "存在未匹配导入行。"
                    : "凭证生成批次记录正常。",
            PersistedResult =
            {
                ["voucherRecordCount"] = summary.RecordCount,
                ["matchedRows"] = summary.MatchedRows,
                ["unmatchedRows"] = summary.UnmatchedRows,
                ["generatedVoucherCount"] = summary.GeneratedVoucherCount
            }
        };
    }

    /// <summary>凭证行级抽样结果：状态判定 + 三段式核验明细装配</summary>
    private static ImportValidationSampleResultDto BuildVoucherRowResult(VoucherExplainSnapshot explain)
    {
        var detail = new ImportValidationVoucherDetailDto
        {
            PassedFilter = explain.PassedFilter,
            Matched = explain.Matched,
            MatchedLayer = explain.MatchedLayer,
            MatchReason = explain.MatchReason,
            RoutedButNoOutput = explain.RoutedButNoOutput,
            RuleGroupName = explain.RuleGroupName,
            AmountAggregation = explain.AmountAggregation,
            SourceFieldValues = explain.SourceFieldValues,
            RuleLines = explain.RuleLines,
            DraftEntries = explain.DraftEntries,
            DraftDebitTotal = explain.DraftDebitTotal,
            DraftCreditTotal = explain.DraftCreditTotal,
            DraftBalanced = explain.DraftBalanced,
            BusinessKey = explain.BusinessKey,
            ActualVoucherId = explain.ActualVoucherId,
            ActualVoucherNo = explain.ActualVoucherNo,
            ActualEntries = explain.ActualEntries,
            ActualDebitTotal = explain.ActualDebitTotal,
            ActualCreditTotal = explain.ActualCreditTotal,
            ActualBalanced = explain.ActualBalanced,
            Issues = explain.Issues
        };

        string status;
        string message;

        if (!explain.PassedFilter)
        {
            status = "ok";
            message = "该行被预筛选条件排除，不参与凭证生成（符合规则配置）。";
        }
        else if (!explain.Matched)
        {
            status = explain.RoutedButNoOutput ? "error" : "missing";
            message = explain.Issues.FirstOrDefault() ?? "凭证规则未命中该行。";
        }
        else if (explain.DraftEntries.Any(e => e.Issue != null))
        {
            status = "error";
            message = explain.Issues.FirstOrDefault() ?? "分录草案存在问题。";
        }
        else if (explain.DraftBalanced == false)
        {
            status = "error";
            message = $"凭证草案借贷不平：借 {explain.DraftDebitTotal} / 贷 {explain.DraftCreditTotal}。";
        }
        else if (explain.ActualVoucherId != null)
        {
            var amountMatches = explain.ActualDebitTotal.HasValue
                && Math.Abs(explain.ActualDebitTotal.Value - explain.DraftDebitTotal) <= 0.01m;
            if (explain.ActualBalanced == false)
            {
                status = "different";
                message = $"实际凭证 {explain.ActualVoucherNo} 借贷不平（借 {explain.ActualDebitTotal} / 贷 {explain.ActualCreditTotal}）。";
            }
            else if (!amountMatches)
            {
                status = "different";
                message = $"草案借方合计 {explain.DraftDebitTotal} 与实际凭证 {explain.ActualVoucherNo} 借方合计 {explain.ActualDebitTotal} 不一致。";
            }
            else
            {
                status = "ok";
                message = $"凭证草案与实际凭证 {explain.ActualVoucherNo} 一致。";
            }
        }
        else if (!string.IsNullOrWhiteSpace(explain.BusinessKey))
        {
            status = "missing";
            message = "凭证草案正常，但按业务键未找到对应实际凭证。";
        }
        else
        {
            // SUM 模式：行级核对到规则命中与分录结构，凭证级金额由整组聚合
            status = "ok";
            message = $"规则命中正常（{explain.AmountAggregation} 聚合模式，凭证金额按整组聚合，行级核对至分录结构）。";
        }

        return new ImportValidationSampleResultDto
        {
            Domain = ValidationDomain.Voucher,
            Label = "自动凭证",
            OriginalValue = explain.RuleGroupName,
            SystemValue = explain.ActualVoucherNo ?? (explain.Matched ? $"草案借 {explain.DraftDebitTotal}" : null),
            ExpectedValue = explain.Matched ? explain.DraftDebitTotal : null,
            Difference = explain.ActualDebitTotal.HasValue && explain.Matched
                ? explain.ActualDebitTotal.Value - explain.DraftDebitTotal
                : null,
            Status = status,
            Message = message,
            VoucherDetail = detail,
            PersistedResult =
            {
                ["ruleGroup"] = explain.RuleGroupName,
                ["matchedLayer"] = explain.MatchedLayer,
                ["businessKey"] = explain.BusinessKey,
                ["actualVoucherId"] = explain.ActualVoucherId,
                ["actualVoucherNo"] = explain.ActualVoucherNo
            },
            TraceSteps = VoucherExplainService.BuildTraceSteps(explain)
        };
    }

    private ImportValidationSampleResultDto BuildPricingSampleResult(
        IReadOnlyDictionary<string, object?> sourceFields,
        string? waybillNo,
        IReadOnlyDictionary<string, PricingSampleResult> pricingResults,
        IReadOnlyDictionary<string, PricingExplainSnapshot> pricingExplains,
        ValidationBatchContext context,
        List<ImportValidationFindingDto> explainFindingCollector)
    {
        var originalValue = GetValue(sourceFields, "F应收金额", "F收入", "F付款金额", "F报销总额");
        var originalDecimal = ToDecimal(originalValue);

        PricingExplainSnapshot? explain = null;
        if (!string.IsNullOrWhiteSpace(waybillNo))
            pricingExplains.TryGetValue(waybillNo, out explain);

        if (string.IsNullOrWhiteSpace(waybillNo) || !pricingResults.TryGetValue(waybillNo, out var pricing))
        {
            var missing = new ImportValidationSampleResultDto
            {
                Domain = ValidationDomain.Pricing,
                Label = "价格计算",
                OriginalValue = originalValue,
                Status = "missing",
                Message = string.IsNullOrWhiteSpace(waybillNo)
                    ? "原始行缺少可用于匹配价格结果的运单号。"
                    : "未找到该行对应的价格计算结果。"
            };

            if (explain != null)
            {
                missing.TraceSteps.AddRange(BuildExplainTraceSteps(explain));
                if (explain.Success)
                {
                    missing.ExpectedValue = explain.TotalChargeAmount;
                    missing.Message = $"未找到该行对应的价格计算结果，但按当前配置解释可算出应收合计 {explain.TotalChargeAmount}。";

                    // 批次执行中结果未写完属正常，不归因写入链路
                    if (!context.IsBatchRunning)
                    {
                        explainFindingCollector.Add(new ImportValidationFindingDto
                        {
                            Domain = ValidationDomain.Pricing,
                            Attribution = ValidationAttribution.Persistence,
                            Severity = ValidationSeverity.High,
                            Confidence = 0.8m,
                            WaybillNo = waybillNo,
                            BusinessKey = waybillNo,
                            Title = "价格解释正常但计费结果缺失",
                            Message = $"运单 {waybillNo} 按当前配置可算出应收合计 {explain.TotalChargeAmount}，但计费结果表无记录。",
                            ExpectedValue = explain.TotalChargeAmount,
                            SystemValue = null,
                            SuggestedAction = "检查计费节点是否执行、批次/组织过滤与结果写入链路；必要时重跑价格计算。",
                            Evidence = new ImportValidationEvidenceDto
                            {
                                TraceSteps = BuildExplainTraceSteps(explain)
                            }
                        });
                    }
                }
            }

            return missing;
        }

        decimal? difference = originalDecimal.HasValue && pricing.ChargeAmount.HasValue
            ? pricing.ChargeAmount.Value - originalDecimal.Value
            : null;

        var result = new ImportValidationSampleResultDto
        {
            Domain = ValidationDomain.Pricing,
            Label = "价格计算",
            OriginalValue = originalValue,
            SystemValue = pricing.ChargeAmount,
            ExpectedValue = originalValue,
            Difference = difference,
            Status = pricing.CalcStatus == 1 && string.IsNullOrWhiteSpace(pricing.ErrorMessage)
                ? HasMaterialDifference(difference) ? "different" : "ok"
                : "error",
            Message = string.IsNullOrWhiteSpace(pricing.ErrorMessage)
                ? "价格计算结果已写入。"
                : pricing.ErrorMessage,
            PersistedResult =
            {
                ["billingResultId"] = pricing.Id,
                ["calcStatus"] = pricing.CalcStatus,
                ["chargeAmount"] = pricing.ChargeAmount,
                ["billingWeight"] = pricing.BillingWeight,
                ["quotationCode"] = pricing.QuotationCode,
                ["clientType"] = pricing.ClientType
            },
            TraceSteps =
            {
                new CalculationTraceStepDto
                {
                    Step = "source-vs-pricing",
                    Description = "对比导入原始应收金额与价格计算写入结果。",
                    InputValue = originalDecimal,
                    OutputValue = pricing.ChargeAmount,
                    Formula = "计费结果.F应收金额 - 导入行.F应收金额"
                }
            }
        };

        if (explain == null)
            return result;

        result.TraceSteps.AddRange(BuildExplainTraceSteps(explain));

        var calcSucceeded = pricing.CalcStatus == 1 && string.IsNullOrWhiteSpace(pricing.ErrorMessage);
        if (calcSucceeded && explain.Success)
        {
            // 解释值优先取与系统记录相同业务对象的步骤，回退到首个命中步骤
            var explainStep = explain.Steps.FirstOrDefault(s =>
                    !string.IsNullOrWhiteSpace(pricing.ClientType)
                    && string.Equals(s.ClientType, pricing.ClientType, StringComparison.OrdinalIgnoreCase)
                    && s.ChargeAmount.HasValue)
                ?? explain.Steps.FirstOrDefault(s => s.ChargeAmount.HasValue);

            if (explainStep?.ChargeAmount != null && pricing.ChargeAmount.HasValue)
            {
                var explainDiff = pricing.ChargeAmount.Value - explainStep.ChargeAmount.Value;
                result.ExpectedValue = explainStep.ChargeAmount;
                result.Difference = explainDiff;

                if (Math.Abs(explainDiff) > context.Tolerance)
                {
                    result.Status = "different";
                    result.Message = $"系统应收 {pricing.ChargeAmount} 与按当前配置的解释值 {explainStep.ChargeAmount} 差异 {explainDiff}，超出容差 {context.Tolerance}。";

                    explainFindingCollector.Add(new ImportValidationFindingDto
                    {
                        Domain = ValidationDomain.Pricing,
                        Attribution = ValidationAttribution.CalculationLogic,
                        Severity = ValidationSeverity.High,
                        Confidence = 0.7m,
                        WaybillNo = waybillNo,
                        BusinessKey = waybillNo,
                        Title = "价格解释值与系统值不一致",
                        Message = $"运单 {waybillNo}（{explainStep.ClientType} 报价 {explainStep.QuotationCode}）系统应收 {pricing.ChargeAmount}，解释值 {explainStep.ChargeAmount}，差异 {explainDiff}。可能为计算逻辑问题，也可能是计费后报价配置已变更。",
                        SystemValue = pricing.ChargeAmount,
                        ExpectedValue = explainStep.ChargeAmount,
                        Difference = explainDiff,
                        SuggestedAction = "核对该报价的矩阵/附加费近期是否调整；未调整则提交开发按公式与输入值核查计算逻辑。",
                        Evidence = new ImportValidationEvidenceDto
                        {
                            TraceSteps = BuildExplainTraceSteps(explain),
                            PersistedResult =
                            {
                                ["billingResultId"] = pricing.Id,
                                ["chargeAmount"] = pricing.ChargeAmount,
                                ["quotationCode"] = pricing.QuotationCode
                            }
                        }
                    });
                }
                else
                {
                    result.Message = "价格计算结果已写入，且与按当前配置的解释值一致。";
                }
            }
        }
        else if (!calcSucceeded && explain.Success)
        {
            result.Message = $"{result.Message}（按当前配置解释已可算出价格，配置可能已补全，可重跑计费。）";
        }

        return result;
    }

    private static List<CalculationTraceStepDto> BuildExplainTraceSteps(PricingExplainSnapshot explain)
    {
        var steps = new List<CalculationTraceStepDto>();

        foreach (var step in explain.Steps)
        {
            if (!step.QuotationMatched)
            {
                steps.Add(new CalculationTraceStepDto
                {
                    Step = $"explain-{step.ClientType}",
                    Description = $"{step.ClientType}：未命中报价" +
                        (step.ConfigurationIssues.Count > 0 ? $"（{string.Join("；", step.ConfigurationIssues)}）" : string.Empty)
                });
                continue;
            }

            var description = $"{step.ClientType}：报价 {step.QuotationCode}";
            if (!step.SegmentMatched)
                description += "，未命中重量段";
            else if (!step.PriceCellMatched)
                description += $"，重量段 #{step.SegmentIndex} 未命中价格矩阵单元格";
            else
                description += $"，重量段 #{step.SegmentIndex}，运费 {step.Freight} + 附加费 {step.Surcharge} + 保价费 {step.InsuranceFee}" +
                    (step.CommissionAmount.HasValue ? $"，佣金 {step.CommissionAmount}" : string.Empty);

            steps.Add(new CalculationTraceStepDto
            {
                Step = $"explain-{step.ClientType}",
                Description = description,
                InputValue = step.BillableWeight,
                OutputValue = step.ChargeAmount,
                Formula = step.FormulaText
            });
        }

        if (!explain.Success && !string.IsNullOrWhiteSpace(explain.ErrorMessage))
        {
            steps.Add(new CalculationTraceStepDto
            {
                Step = "explain-error",
                Description = $"解释失败：{explain.ErrorCode} {explain.ErrorMessage}"
            });
        }

        return steps;
    }

    private static ImportValidationSampleResultDto BuildCostSampleResult(
        IReadOnlyDictionary<string, object?> sourceFields,
        string? waybillNo,
        IReadOnlyDictionary<string, CostSampleResult> costResults)
    {
        var originalValue = GetValue(sourceFields, "F成本合计", "F成本", "F支出金额");

        if (string.IsNullOrWhiteSpace(waybillNo) || !costResults.TryGetValue(waybillNo, out var cost))
        {
            return new ImportValidationSampleResultDto
            {
                Domain = ValidationDomain.Cost,
                Label = "成本计算",
                OriginalValue = originalValue,
                Status = "missing",
                Message = string.IsNullOrWhiteSpace(waybillNo)
                    ? "原始行缺少可用于匹配成本结果的运单号。"
                    : "未找到该行对应的成本计算结果。"
            };
        }

        var difference = cost.TotalCost - cost.BreakdownTotal;
        return new ImportValidationSampleResultDto
        {
            Domain = ValidationDomain.Cost,
            Label = "成本计算",
            OriginalValue = originalValue,
            SystemValue = cost.TotalCost,
            ExpectedValue = cost.BreakdownTotal,
            Difference = difference,
            Status = cost.BreakdownCount == 0
                ? "missing"
                : HasMaterialDifference(difference) ? "different" : "ok",
            Message = cost.BreakdownCount == 0
                ? "未找到成本明细。"
                : "成本方案各成本项结果与合计已写入。",
            CostItems = cost.CostItems
                .Select(item => new ImportValidationSampleCostItemDto
                {
                    CostItemId = item.CostItemId,
                    CostItemName = item.CostItemName,
                    Amount = item.Amount
                })
                .ToList(),
            PersistedResult =
            {
                ["billingResultId"] = cost.BillingResultId,
                ["totalCost"] = cost.TotalCost,
                ["breakdownTotal"] = cost.BreakdownTotal,
                ["breakdownCount"] = cost.BreakdownCount,
                ["costItems"] = cost.CostItems
                    .Select(item => new Dictionary<string, object?>
                    {
                        ["costItemId"] = item.CostItemId,
                        ["costItemName"] = item.CostItemName,
                        ["amount"] = item.Amount
                    })
                    .ToList()
            },
            TraceSteps =
            {
                new CalculationTraceStepDto
                {
                    Step = "cost-breakdown-sum",
                    Description = "汇总计费结果下的成本明细金额，与计费结果成本合计对比。",
                    InputValue = cost.BreakdownTotal,
                    OutputValue = cost.TotalCost,
                    Formula = "SUM(成本明细.F金额) == 计费结果.F成本合计"
                }
            }
        };
    }

    private static List<ImportValidationFindingDto> MatchFindings(
        IReadOnlyList<ImportValidationFindingDto> findings,
        long? sourceRowId,
        string? waybillNo,
        string? businessKey)
    {
        return findings
            .Where(finding =>
                (sourceRowId.HasValue && finding.SourceRowId == sourceRowId)
                || (!string.IsNullOrWhiteSpace(waybillNo)
                    && string.Equals(finding.WaybillNo, waybillNo, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(businessKey)
                    && string.Equals(finding.BusinessKey, businessKey, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static List<ImportValidationFindingDto> BuildRepresentativeFindings(
        IEnumerable<ImportValidationFindingDto> findings)
    {
        return findings
            .GroupBy(BuildFindingSignature, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var representative = group
                    .OrderByDescending(finding => finding.Severity)
                    .ThenBy(finding => finding.Domain)
                    .ThenBy(finding => finding.SourceRowId ?? long.MaxValue)
                    .First();
                representative.AffectedRows = group.Sum(finding => Math.Max(1, finding.AffectedRows));
                return representative;
            })
            .ToList();
    }

    private static string BuildFindingSignature(ImportValidationFindingDto finding)
    {
        return string.Join("|",
            finding.Domain,
            finding.Attribution,
            finding.Title.Trim(),
            NormalizeIssueMessage(finding.Message));
    }

    private static string NormalizeIssueMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var normalized = message.Trim();
        var codeSeparator = normalized.IndexOf(':');
        if (codeSeparator > 0)
            return normalized[..codeSeparator].Trim();

        return normalized;
    }

    private static List<SourceSampleRow> OrderSourceRowsByWaybillNos(
        IEnumerable<SourceSampleRow> rows,
        IReadOnlyList<string> waybillNos)
    {
        var order = waybillNos
            .Select((waybillNo, index) => new { waybillNo, index })
            .GroupBy(item => item.waybillNo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);

        return rows
            .OrderBy(row =>
            {
                var waybillNo = GetString(row.Fields, "F运单编号", "F运单号", "F单号", "F关联单号");
                return !string.IsNullOrWhiteSpace(waybillNo) && order.TryGetValue(waybillNo, out var index)
                    ? index
                    : int.MaxValue;
            })
            .ThenBy(row => row.SourceRowId ?? long.MaxValue)
            .ToList();
    }

    private static void AddDistinctWaybillNos(
        List<string> selected,
        HashSet<string> seen,
        IEnumerable<string> candidates,
        int limit)
    {
        foreach (var candidate in candidates)
        {
            if (selected.Count >= limit)
                return;

            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
                continue;

            selected.Add(candidate);
        }
    }

    private static int BuildCandidateQueryLimit(int limit, int excludedCount)
    {
        return Math.Clamp(Math.Max(limit * 2, limit + excludedCount), 1, 5000);
    }

    private static Dictionary<string, object?> ToDictionary(object row)
    {
        if (row is IDictionary<string, object?> nullableDict)
            return nullableDict.ToDictionary(pair => pair.Key, pair => NormalizeValue(pair.Value));

        if (row is IDictionary<string, object> dict)
            return dict.ToDictionary(pair => pair.Key, pair => NormalizeValue(pair.Value));

        return row.GetType()
            .GetProperties()
            .ToDictionary(property => property.Name, property => NormalizeValue(property.GetValue(row)));
    }

    private static object? NormalizeValue(object? value) => value == DBNull.Value ? null : value;

    private static string QuoteSqlIdentifierPath(string identifier)
    {
        var parts = identifier
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("目标暂存表名无效。");

        return string.Join(".", parts.Select(part => $"[{part.Replace("]", "]]")}]"));
    }

    private static string QuoteSqlIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new InvalidOperationException("SQL 字段名无效。");

        return $"[{identifier.Replace("]", "]]")}]";
    }

    private static int GetLimit(ImportValidationRunRequest request)
    {
        if (IsAllMode(request))
            return 5000;

        return Math.Clamp(request.SampleSize, 1, 5000);
    }

    private static IEnumerable<string[]> ChunkWaybillNos(IEnumerable<string> sourceWaybillNos)
    {
        return sourceWaybillNos
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Chunk(500);
    }

    private static object? GetValue(IReadOnlyDictionary<string, object?> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var value))
                return value;
        }

        return null;
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> fields, params string[] keys)
    {
        var value = GetValue(fields, keys);
        return string.IsNullOrWhiteSpace(value?.ToString()) ? null : value.ToString();
    }

    private static long? GetLong(IReadOnlyDictionary<string, object?> fields, params string[] keys)
    {
        var value = GetValue(fields, keys);
        if (value == null) return null;
        return long.TryParse(value.ToString(), out var result) ? result : null;
    }

    private static decimal? ToDecimal(object? value)
    {
        if (value == null) return null;
        if (value is decimal decimalValue) return decimalValue;
        if (value is double doubleValue) return Convert.ToDecimal(doubleValue);
        if (value is float floatValue) return Convert.ToDecimal(floatValue);
        if (value is int intValue) return intValue;
        if (value is long longValue) return longValue;
        return decimal.TryParse(value.ToString(), out var result) ? result : null;
    }

    private static bool HasMaterialDifference(decimal? difference)
    {
        return difference.HasValue && Math.Abs(difference.Value) > 0.01m;
    }

    private static readonly string[] SourceWaybillColumns =
    [
        "F运单编号",
        "F运单号",
        "F单号",
        "F关联单号"
    ];

    private sealed class SourceSampleRow
    {
        public long? SourceRowId { get; init; }
        public Dictionary<string, object?> Fields { get; init; } = new();
    }

    private sealed class PricingSampleResult
    {
        public long Id { get; init; }
        public string? WaybillNo { get; init; }
        public int CalcStatus { get; init; }
        public string? ErrorMessage { get; init; }
        public decimal? ChargeAmount { get; init; }
        public decimal? BillingWeight { get; init; }
        public string? QuotationCode { get; init; }
        public string? ClientType { get; init; }
    }

    private sealed class CostSampleResult
    {
        public long BillingResultId { get; init; }
        public string? WaybillNo { get; init; }
        public decimal TotalCost { get; init; }
        public decimal BreakdownTotal { get; init; }
        public int BreakdownCount { get; init; }
        public List<CostItemSampleResult> CostItems { get; init; } = [];
    }

    private sealed class CostSampleLine
    {
        public long BillingResultId { get; init; }
        public string? WaybillNo { get; init; }
        public decimal TotalCost { get; init; }
        public long? CostBreakdownId { get; init; }
        public int? CostItemId { get; init; }
        public string? CostItemName { get; init; }
        public decimal? CostAmount { get; init; }
    }

    private sealed class CostItemSampleResult
    {
        public int CostItemId { get; init; }
        public string CostItemName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }

    private sealed class VoucherSampleSummary
    {
        public int RecordCount { get; init; }
        public int TotalRows { get; init; }
        public int MatchedRows { get; init; }
        public int UnmatchedRows { get; init; }
        public int GeneratedVoucherCount { get; init; }
        public bool HasError { get; init; }
        public string? FirstErrorMessage { get; init; }
    }
}

public class ValidationBatchContext
{
    public long BatchId { get; init; }
    public long FlowDefinitionId { get; init; }
    public long OrgId { get; init; }
    public string? BatchNo { get; init; }
    public string? TargetTable { get; init; }
    public int TotalRows { get; init; }
    public decimal Tolerance { get; init; } = 0.01m;
    /// <summary>批次状态机：0=解析中, 1=已暂存, 2=质检中, 3=已创建卡片, 4=处理中, 5=已完成</summary>
    public int BatchStatus { get; init; }
    public bool IsRevoked { get; init; }
    public string? CurrentNodeName { get; init; }
    public int? ProgressPercent { get; init; }
    public string? BatchErrorMessage { get; init; }
    public DateTime? ImportStartTime { get; init; }
    public DateTime? ImportEndTime { get; init; }

    /// <summary>节点链尚未执行完成（计费/成本结果可能还没写入）</summary>
    public bool IsBatchRunning => !IsRevoked && BatchStatus < 5;
}
