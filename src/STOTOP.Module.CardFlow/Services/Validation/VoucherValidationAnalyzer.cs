using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.CardFlow.Services.Validation;

public class VoucherValidationAnalyzer
{
    private readonly STOTOPDbContext _db;
    private readonly ValidationAttributionClassifier _classifier;

    public VoucherValidationAnalyzer(STOTOPDbContext db, ValidationAttributionClassifier classifier)
    {
        _db = db;
        _classifier = classifier;
    }

    public Task<int> CountAsync(long batchId, CancellationToken cancellationToken = default)
    {
        return _db.Set<CfVoucherRecord>()
            .AsNoTracking()
            .CountAsync(record => record.FBatchId == batchId, cancellationToken);
    }

    public async Task<IReadOnlyList<ImportValidationFindingDto>> AnalyzeAsync(
        ValidationBatchContext context,
        ImportValidationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var records = await _db.Set<CfVoucherRecord>()
            .AsNoTracking()
            .Where(record => record.FBatchId == context.BatchId)
            .OrderBy(record => record.FID)
            .Take(GetLimit(request))
            .ToListAsync(cancellationToken);

        if (records.Count == 0 && context.TotalRows > 0)
        {
            var result = _classifier.Classify(new ValidationClassificationInput
            {
                ExpectedValue = context.TotalRows,
                PersistedResultExists = false
            });

            return
            [
                new ImportValidationFindingDto
                {
                    Domain = ValidationDomain.Voucher,
                    Attribution = result.Attribution,
                    Severity = result.Severity,
                    Confidence = result.Confidence,
                    Title = "未找到凭证生成记录",
                    Message = "该批次已有导入行，但未找到自动生成凭证的执行记录。",
                    ExpectedValue = context.TotalRows,
                    SystemValue = 0,
                    Difference = context.TotalRows,
                    SuggestedAction = result.SuggestedAction,
                    Evidence = new ImportValidationEvidenceDto
                    {
                        PersistedResult =
                        {
                            ["batchId"] = context.BatchId,
                            ["targetTable"] = context.TargetTable
                        }
                    }
                }
            ];
        }

        var findings = new List<ImportValidationFindingDto>();
        foreach (var record in records)
        {
            if (record.FUnmatchedRows > 0)
            {
                var result = _classifier.Classify(new ValidationClassificationInput
                {
                    ConfigurationMatched = false,
                    ConfigurationIssues = ["自动凭证规则存在未匹配导入行"],
                    PersistedResultExists = true
                });

                findings.Add(new ImportValidationFindingDto
                {
                    Domain = ValidationDomain.Voucher,
                    Attribution = result.Attribution,
                    Severity = result.Severity,
                    Confidence = result.Confidence,
                    SourceRowId = record.FID,
                    Title = "凭证匹配规则未覆盖导入数据",
                    Message = $"自动凭证记录存在 {record.FUnmatchedRows} 行未匹配，生成凭证数 {record.FGeneratedVoucherCount}。",
                    ExpectedValue = record.FTotalRows,
                    SystemValue = record.FMatchedRows,
                    Difference = record.FUnmatchedRows,
                    SuggestedAction = result.SuggestedAction,
                    Evidence = new ImportValidationEvidenceDto
                    {
                        ConfigurationIssues = ["自动凭证规则未覆盖所有导入行"],
                        PersistedResult =
                        {
                            ["voucherRecordId"] = record.FID,
                            ["targetTable"] = record.FTargetTable,
                            ["unmatchedDetails"] = request.IncludeEvidence ? record.FUnmatchedDetailsJson : null
                        }
                    }
                });
            }

            var balanceFinding = await AnalyzeVoucherBalanceAsync(record, context, cancellationToken);
            if (balanceFinding != null)
                findings.Add(balanceFinding);

            if (record.FStatus == 3 || !string.IsNullOrWhiteSpace(record.FErrorMessage))
            {
                var result = _classifier.Classify(new ValidationClassificationInput
                {
                    ConfigurationMatched = record.FUnmatchedRows == 0,
                    ConfigurationIssues = record.FUnmatchedRows > 0 ? ["自动凭证执行失败且存在规则未匹配"] : [],
                    ExpectedValue = record.FMatchedRows,
                    PersistedResultExists = record.FGeneratedVoucherCount > 0
                });

                findings.Add(new ImportValidationFindingDto
                {
                    Domain = ValidationDomain.Voucher,
                    Attribution = result.Attribution == ValidationAttribution.None
                        ? ValidationAttribution.Persistence
                        : result.Attribution,
                    Severity = record.FStatus == 3 ? ValidationSeverity.Blocker : result.Severity,
                    Confidence = result.Confidence,
                    SourceRowId = record.FID,
                    Title = "凭证生成执行异常",
                    Message = string.IsNullOrWhiteSpace(record.FErrorMessage)
                        ? "自动凭证执行失败。"
                        : record.FErrorMessage,
                    ExpectedValue = record.FMatchedRows,
                    SystemValue = record.FGeneratedVoucherCount,
                    Difference = record.FMatchedRows - record.FGeneratedVoucherCount,
                    SuggestedAction = result.SuggestedAction,
                    Evidence = new ImportValidationEvidenceDto
                    {
                        PersistedResult =
                        {
                            ["voucherRecordId"] = record.FID,
                            ["status"] = record.FStatus,
                            ["voucherIds"] = request.IncludeEvidence ? record.FVoucherIdsJson : null
                        }
                    }
                });
            }
        }

        return findings;
    }

    /// <summary>
    /// 逐凭证核对实际凭证借贷平衡：按生成记录的凭证ID列表聚合 FIN凭证分录，
    /// 借贷差超容差即"DryRun 平衡但实际不平"类问题（疑似计算逻辑/写入链路）。
    /// </summary>
    private async Task<ImportValidationFindingDto?> AnalyzeVoucherBalanceAsync(
        CfVoucherRecord record,
        ValidationBatchContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.FVoucherIdsJson))
            return null;

        List<long> voucherIds;
        try
        {
            voucherIds = JsonSerializer.Deserialize<List<long>>(record.FVoucherIdsJson) ?? [];
        }
        catch (JsonException)
        {
            return null;
        }

        if (voucherIds.Count == 0)
            return null;

        // 限额核对，防止 ROW 模式数万凭证拖垮验证接口
        const int maxVouchersToCheck = 5000;
        var checkedIds = voucherIds.Take(maxVouchersToCheck).ToList();

        var unbalanced = await _db.Set<FinVoucherEntry>()
            .AsNoTracking()
            .Where(e => checkedIds.Contains(e.FVoucherId))
            .GroupBy(e => e.FVoucherId)
            .Select(g => new
            {
                VoucherId = g.Key,
                DebitTotal = g.Sum(e => e.FDebitAmount),
                CreditTotal = g.Sum(e => e.FCreditAmount)
            })
            .Where(v => v.DebitTotal - v.CreditTotal > context.Tolerance
                || v.CreditTotal - v.DebitTotal > context.Tolerance)
            .OrderBy(v => v.VoucherId)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (unbalanced.Count == 0)
            return null;

        var sample = unbalanced[0];
        var result = _classifier.Classify(new ValidationClassificationInput
        {
            ExpectedValue = sample.DebitTotal,
            SystemValue = sample.CreditTotal,
            Tolerance = context.Tolerance,
            PersistedResultExists = true
        });

        return new ImportValidationFindingDto
        {
            Domain = ValidationDomain.Voucher,
            Attribution = ValidationAttribution.CalculationLogic,
            Severity = ValidationSeverity.Blocker,
            Confidence = result.Confidence,
            AffectedRows = unbalanced.Count,
            SourceRowId = record.FID,
            VoucherId = sample.VoucherId,
            Title = "实际凭证借贷不平",
            Message = $"生成记录 {record.FID} 关联的凭证中发现 {unbalanced.Count} 张借贷不平（核对上限 {maxVouchersToCheck} 张）。样例凭证 {sample.VoucherId}：借 {sample.DebitTotal} / 贷 {sample.CreditTotal}。",
            SystemValue = sample.CreditTotal,
            ExpectedValue = sample.DebitTotal,
            Difference = sample.DebitTotal - sample.CreditTotal,
            SuggestedAction = "借贷不平凭证不应通过生成校验，请提交开发核查凭证写入链路；确认后对受影响凭证执行红冲重做。",
            Evidence = new ImportValidationEvidenceDto
            {
                PersistedResult =
                {
                    ["voucherRecordId"] = record.FID,
                    ["unbalancedVouchers"] = unbalanced
                        .Select(v => new Dictionary<string, object?>
                        {
                            ["voucherId"] = v.VoucherId,
                            ["debitTotal"] = v.DebitTotal,
                            ["creditTotal"] = v.CreditTotal
                        })
                        .ToList(),
                    ["checkedVoucherCount"] = checkedIds.Count,
                    ["totalVoucherCount"] = voucherIds.Count
                }
            }
        };
    }

    private static int GetLimit(ImportValidationRunRequest request)
    {
        if (string.Equals(request.Mode, "all", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Mode, "allLimited", StringComparison.OrdinalIgnoreCase))
            return 5000;

        return Math.Clamp(request.SampleSize, 1, 5000);
    }
}
