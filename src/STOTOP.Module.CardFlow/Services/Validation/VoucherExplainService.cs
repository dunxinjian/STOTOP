using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Handlers;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.CardFlow.Services.Validation;

/// <summary>
/// 自动凭证行级解释：对抽样行复用 AutoVoucherMatchingEngineV2 / AutoVoucherHandler
/// 同一套规则配置与方法，逐行输出 预筛选→三层命中→规则组→分录草案（方向/科目/金额/摘要）→借贷平衡，
/// 并在 ROW 聚合模式下按业务键反查 FinVoucher 与实际分录对账。
/// 解释失败不抛异常，验证主流程降级为记录级校验。
/// </summary>
public class VoucherExplainService
{
    /// <summary>自动凭证插件在 CF自动插件注册 中的编码</summary>
    private const string AutoVoucherPluginCode = "AutoVoucher";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly STOTOPDbContext _db;
    private readonly ILogger<VoucherExplainService> _logger;

    public VoucherExplainService(STOTOPDbContext db, ILogger<VoucherExplainService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<VoucherExplainBatchResult> ExplainAsync(
        ValidationBatchContext context,
        IReadOnlyList<VoucherExplainSourceRow> sampleRows,
        CancellationToken cancellationToken = default)
    {
        var result = new VoucherExplainBatchResult();
        if (sampleRows.Count == 0)
            return result;

        try
        {
            var config = await ResolveVoucherConfigAsync(context.FlowDefinitionId, cancellationToken);
            if (config == null || config.RuleGroups.Count == 0)
                return result;

            result.HasConfig = true;

            var engine = new AutoVoucherMatchingEngineV2();
            engine.Initialize(config);

            foreach (var sourceRow in sampleRows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = ExplainSingleRow(engine, config, context.BatchId, sourceRow);
                if (snapshot.SourceRowId.HasValue)
                    result.Rows[snapshot.SourceRowId.Value] = snapshot;
            }

            await FillAccountNamesAsync(result, cancellationToken);
            await ReconcileWithActualVouchersAsync(context, result, cancellationToken);
            BuildFindings(context, result);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "凭证行级解释失败（批次={BatchId}），验证降级为记录级校验", context.BatchId);
            return new VoucherExplainBatchResult();
        }
    }

    /// <summary>
    /// 按流程定义找到自动凭证节点的规则配置。
    /// 与价格解释一致：取当前版本节点链；历史版本批次按当前配置解释（验证台定位的是"现在该怎么算"）。
    /// </summary>
    private async Task<RulesBasedVoucherConfigV2?> ResolveVoucherConfigAsync(
        long flowDefinitionId,
        CancellationToken cancellationToken)
    {
        var registryIds = await _db.Set<CfAutoPluginRegistry>()
            .AsNoTracking()
            .Where(r => r.F插件编码 == AutoVoucherPluginCode)
            .Select(r => r.FID)
            .ToListAsync(cancellationToken);

        if (registryIds.Count == 0)
            return null;

        var versionId = await _db.Set<CfFlowVersion>()
            .AsNoTracking()
            .Where(v => v.FFlowDefinitionId == flowDefinitionId && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .Select(v => (long?)v.FID)
            .FirstOrDefaultAsync(cancellationToken);

        if (versionId == null)
            return null;

        var ruleId = await _db.Set<CfStageDefinition>()
            .AsNoTracking()
            .Where(s => s.FFlowVersionId == versionId.Value
                && s.F插件注册ID != null
                && registryIds.Contains(s.F插件注册ID.Value)
                && s.F插件规则ID != null)
            .OrderBy(s => s.FSortOrder)
            .Select(s => s.F插件规则ID)
            .FirstOrDefaultAsync(cancellationToken);

        if (ruleId == null)
            return null;

        var ruleJson = await _db.Set<CfPluginRule>()
            .AsNoTracking()
            .Where(r => r.FID == ruleId.Value)
            .Select(r => r.F规则配置JSON)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(ruleJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(ruleJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "自动凭证规则配置反序列化失败（流程={FlowDefinitionId}）", flowDefinitionId);
            return null;
        }
    }

    private static VoucherExplainSnapshot ExplainSingleRow(
        AutoVoucherMatchingEngineV2 engine,
        RulesBasedVoucherConfigV2 config,
        long batchId,
        VoucherExplainSourceRow sourceRow)
    {
        var snapshot = new VoucherExplainSnapshot { SourceRowId = sourceRow.SourceRowId };

        // 验证服务的源行字段含 null 值，引擎按"字段缺失"语义处理
        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in sourceRow.Fields)
        {
            if (pair.Value != null)
                row[pair.Key] = pair.Value;
        }

        snapshot.PassedFilter = engine.ApplyFilterConditions([row], config.FilterConditions).Count > 0;
        if (!snapshot.PassedFilter)
        {
            snapshot.Issues.Add("被预筛选条件排除，不参与凭证生成。");
            return snapshot;
        }

        var candidates = engine.MatchRowToRuleGroup(row);
        snapshot.CandidateLayers = candidates.Select(c => c.Layer).Distinct().OrderBy(l => l).ToList();

        var (groupId, routedButNoOutput) = engine.ResolveFinalGroup(row, candidates);
        snapshot.RoutedButNoOutput = routedButNoOutput;

        if (groupId == null)
        {
            snapshot.Issues.Add(candidates.Count == 0
                ? "三层匹配（精确编码/分类/摘要关键词）均未命中规则组。"
                : "候选规则组经 Fallthrough 后均无可接纳分录行。");
            return snapshot;
        }

        var group = config.RuleGroups.First(g => g.Id == groupId);
        snapshot.Matched = !routedButNoOutput;
        snapshot.MatchedLayer = candidates.FirstOrDefault(c => c.GroupId == groupId)?.Layer;
        snapshot.RuleGroupId = group.Id;
        snapshot.RuleGroupName = group.Name;
        snapshot.AmountAggregation = string.IsNullOrWhiteSpace(group.AmountAggregation) ? "SUM" : group.AmountAggregation;
        snapshot.MatchReason = BuildMatchReason(config, group, snapshot.MatchedLayer, row);
        CollectSourceFieldValues(snapshot, config, group, row);
        BuildRuleLineSnapshots(snapshot, group);

        if (routedButNoOutput)
        {
            snapshot.Issues.Add($"已路由到规则组[{group.Name}]，但组内无可接纳该行的分录行（条件行不匹配且无兜底行）。");
            return snapshot;
        }

        // 单行视角的分录草案。SUM 模式下金额是"该行贡献额"，凭证级金额由整组聚合，
        // 行级只核对规则命中、金额字段合法性与分录结构。
        var assigned = engine.AssignRowsToEntryLines([row], group);
        foreach (var line in group.Lines.Where(l => l.Status == 1).OrderBy(l => l.DisplayOrder))
        {
            if (!assigned.TryGetValue(line.LineNo, out var lineRows) || lineRows.Count == 0)
                continue;

            var entry = new ImportValidationVoucherEntryDto
            {
                LineNo = line.LineNo,
                Direction = line.Direction,
                AmountField = line.AmountField,
                Summary = AutoVoucherHandler.RenderSummaryTemplateV2(line.SummaryTemplate, row),
                AuxiliaryText = DescribeAuxiliary(line.AuxiliaryConfigs)
            };

            var rawAmount = row.TryGetValue(line.AmountField, out var v) ? v?.ToString() : null;
            if (string.IsNullOrWhiteSpace(rawAmount))
            {
                entry.Issue = $"金额字段[{line.AmountField}]为空。";
                snapshot.Issues.Add($"分录行{line.LineNo}：{entry.Issue}");
            }
            else if (!decimal.TryParse(rawAmount, out var amount))
            {
                entry.Issue = $"金额字段[{line.AmountField}]值\"{rawAmount}\"不是数字。";
                snapshot.Issues.Add($"分录行{line.LineNo}：{entry.Issue}");
            }
            else
            {
                entry.Amount = amount;
            }

            var accountWarnings = new List<string>();
            entry.AccountId = AutoVoucherHandler.ResolveAccountIdV2(line, row, accountWarnings);
            if (entry.AccountId == null)
            {
                entry.Issue = entry.Issue ?? "科目未命中且无兜底科目。";
                snapshot.Issues.AddRange(accountWarnings.Select(w => $"分录行{line.LineNo}：{w}"));
            }

            snapshot.DraftEntries.Add(entry);
        }

        if (snapshot.DraftEntries.Count > 0)
        {
            // [H3] 金额为 0 的分录在生成时跳过，草案合计同口径
            snapshot.DraftDebitTotal = snapshot.DraftEntries
                .Where(e => e.Direction == "借" && e.Amount.HasValue)
                .Sum(e => e.Amount!.Value);
            snapshot.DraftCreditTotal = snapshot.DraftEntries
                .Where(e => e.Direction == "贷" && e.Amount.HasValue)
                .Sum(e => e.Amount!.Value);
        }

        if (IsRowAggregation(snapshot))
        {
            snapshot.DraftBalanced = Math.Abs(snapshot.DraftDebitTotal - snapshot.DraftCreditTotal) <= 0.001m;
            if (snapshot.DraftBalanced == false)
                snapshot.Issues.Add($"单行凭证草案借贷不平：借 {snapshot.DraftDebitTotal} / 贷 {snapshot.DraftCreditTotal}（ROW 模式每行独立成凭证）。");

            snapshot.BusinessKey = AutoVoucherHandler.ComputeBusinessKeyV2([row], config.KeyFields, batchId);
        }

        return snapshot;
    }

    private static bool IsRowAggregation(VoucherExplainSnapshot snapshot)
        => string.Equals(snapshot.AmountAggregation, "ROW", StringComparison.OrdinalIgnoreCase);

    /// <summary>还原"靠什么命中"：层级 + 字段值 + 精确值/最长关键词（与引擎匹配逻辑同口径）</summary>
    private static string? BuildMatchReason(
        RulesBasedVoucherConfigV2 config,
        RuleGroupV2 group,
        int? layer,
        IDictionary<string, object> row)
    {
        static string Snip(string? value)
        {
            var text = value?.Trim() ?? string.Empty;
            return text.Length > 60 ? text[..60] + "…" : text;
        }

        static string? RowValue(IDictionary<string, object> row, string? field)
            => string.IsNullOrEmpty(field) ? null
                : row.TryGetValue(field, out var v) ? v?.ToString()?.Trim() : null;

        static string? LongestHit(string? haystack, List<string>? keywords)
        {
            if (string.IsNullOrEmpty(haystack) || keywords == null)
                return null;
            return keywords
                .Select(k => k?.Trim())
                .Where(k => !string.IsNullOrEmpty(k)
                    && haystack.IndexOf(k!, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(k => k!.Length)
                .FirstOrDefault();
        }

        switch (layer)
        {
            case 1:
            {
                var value = RowValue(row, config.MatchingLayers.ExactMatchField);
                return $"Layer1 精确编码：[{config.MatchingLayers.ExactMatchField}]=\"{Snip(value)}\" 命中规则组编码列表";
            }
            case 2:
            {
                var value = RowValue(row, config.MatchingLayers.CategoryField);
                if (value != null && group.ExactCategories?.Any(c =>
                        string.Equals(c?.Trim(), value, StringComparison.OrdinalIgnoreCase)) == true)
                    return $"Layer2 精确分类：[{config.MatchingLayers.CategoryField}]=\"{Snip(value)}\"";

                var keyword = LongestHit(value, group.CategoryKeywords);
                return $"Layer2 分类关键词：[{config.MatchingLayers.CategoryField}]=\"{Snip(value)}\" 命中关键词\"{keyword}\"";
            }
            case 3:
            {
                var value = RowValue(row, config.MatchingLayers.SummaryField);
                var keyword = LongestHit(value, group.SummaryKeywords);
                return $"Layer3 摘要关键词：[{config.MatchingLayers.SummaryField}]=\"{Snip(value)}\" 命中关键词\"{keyword}\"";
            }
            default:
                return null;
        }
    }

    /// <summary>收集该行参与凭证生成的关键源字段名值，供核验时与原始数据对照</summary>
    private static void CollectSourceFieldValues(
        VoucherExplainSnapshot snapshot,
        RulesBasedVoucherConfigV2 config,
        RuleGroupV2 group,
        IDictionary<string, object> row)
    {
        var fields = new List<string?>
        {
            config.MatchingLayers.ExactMatchField,
            config.MatchingLayers.CategoryField,
            config.MatchingLayers.SummaryField,
            config.DateField,
            config.GroupBy
        };
        if (config.KeyFields != null)
            fields.AddRange(config.KeyFields);
        foreach (var line in group.Lines.Where(l => l.Status == 1))
        {
            fields.Add(line.AmountField);
            fields.Add(line.ConditionField);
            fields.Add(line.AccountMatchField);
        }

        foreach (var field in fields.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f!).Distinct())
            snapshot.SourceFieldValues[field] = row.TryGetValue(field, out var value) ? value : null;
    }

    /// <summary>规则组分录行配置快照（含禁用行），供核验"规则怎么配的"</summary>
    private static void BuildRuleLineSnapshots(VoucherExplainSnapshot snapshot, RuleGroupV2 group)
    {
        foreach (var line in group.Lines.OrderBy(l => l.DisplayOrder))
        {
            string accountText;
            if (line.AccountId.HasValue)
                accountText = $"固定科目 {line.AccountCode ?? line.AccountId.ToString()}";
            else if (!string.IsNullOrEmpty(line.AccountMatchField))
                accountText = $"动态匹配[{line.AccountMatchField}]（{line.AccountMatchRules?.Count ?? 0} 条映射）"
                    + (line.DefaultAccountId.HasValue ? $"，兜底 {line.DefaultAccountId}" : "，无兜底");
            else
                accountText = "未配置科目";

            snapshot.RuleLines.Add(new ImportValidationVoucherRuleLineDto
            {
                LineNo = line.LineNo,
                Direction = line.Direction,
                AccountText = accountText,
                AmountField = line.AmountField,
                SummaryTemplate = line.SummaryTemplate,
                ConditionText = string.IsNullOrEmpty(line.ConditionField)
                    ? "兜底行（接纳剩余行）"
                    : $"{line.ConditionField} ∈ [{string.Join("，", line.ConditionValues ?? [])}]",
                AuxiliaryText = DescribeAuxiliary(line.AuxiliaryConfigs),
                Enabled = line.Status == 1
            });
        }
    }

    private static string? DescribeAuxiliary(List<AuxiliaryConfigV2>? configs)
    {
        if (configs == null || configs.Count == 0)
            return null;

        return string.Join("；", configs.Select(c =>
            string.Equals(c.SourceType, "dynamic", StringComparison.OrdinalIgnoreCase)
                ? $"{c.AuxType}=动态({c.SourceField} by {c.MatchBy})"
                : $"{c.AuxType}=固定({c.FixedValue ?? c.FixedItemCode ?? c.FixedItemId?.ToString() ?? "-"})"));
    }

    private async Task FillAccountNamesAsync(VoucherExplainBatchResult result, CancellationToken cancellationToken)
    {
        var accountIds = result.Rows.Values
            .SelectMany(s => s.DraftEntries)
            .Where(e => e.AccountId.HasValue)
            .Select(e => e.AccountId!.Value)
            .Distinct()
            .ToList();

        if (accountIds.Count == 0)
            return;

        var accounts = await _db.Set<FinAccount>()
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.FID))
            .Select(a => new { a.FID, a.FCode, a.FName })
            .ToListAsync(cancellationToken);

        var byId = accounts.ToDictionary(a => a.FID);
        foreach (var entry in result.Rows.Values.SelectMany(s => s.DraftEntries))
        {
            if (entry.AccountId.HasValue && byId.TryGetValue(entry.AccountId.Value, out var account))
            {
                entry.AccountCode = account.FCode;
                entry.AccountName = account.FName;
            }
        }
    }

    /// <summary>
    /// ROW 模式行级对账：按重算的业务键反查 FinVoucher（FDataScopeId），
    /// 对比草案与实际凭证的借贷合计及平衡性。
    /// </summary>
    private async Task ReconcileWithActualVouchersAsync(
        ValidationBatchContext context,
        VoucherExplainBatchResult result,
        CancellationToken cancellationToken)
    {
        var businessKeys = result.Rows.Values
            .Where(s => !string.IsNullOrWhiteSpace(s.BusinessKey))
            .Select(s => s.BusinessKey!)
            .Distinct()
            .ToList();

        if (businessKeys.Count == 0)
            return;

        var vouchers = await _db.Set<FinVoucher>()
            .AsNoTracking()
            .Where(v => v.FDataScopeId != null && businessKeys.Contains(v.FDataScopeId) && !v.FIsRevoked)
            .Select(v => new
            {
                v.FID,
                v.FDataScopeId,
                v.FVoucherWord,
                v.FVoucherNo,
                Entries = v.Entries
                    .OrderBy(e => e.FLineNo)
                    .Select(e => new
                    {
                        e.FLineNo,
                        e.FSummary,
                        e.FAccountCode,
                        e.FAccountName,
                        e.FDebitAmount,
                        e.FCreditAmount
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var byKey = vouchers
            .GroupBy(v => v.FDataScopeId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in result.Rows.Values)
        {
            if (string.IsNullOrWhiteSpace(snapshot.BusinessKey))
                continue;

            if (!byKey.TryGetValue(snapshot.BusinessKey, out var voucher))
                continue;

            snapshot.ActualVoucherId = voucher.FID;
            snapshot.ActualVoucherNo = $"{voucher.FVoucherWord}-{voucher.FVoucherNo}";
            snapshot.ActualEntries.AddRange(voucher.Entries.Select(e => new ImportValidationVoucherEntryDto
            {
                LineNo = e.FLineNo,
                Direction = e.FDebitAmount != 0 ? "借" : "贷",
                AccountCode = e.FAccountCode,
                AccountName = e.FAccountName,
                Amount = e.FDebitAmount != 0 ? e.FDebitAmount : e.FCreditAmount,
                Summary = e.FSummary
            }));
            snapshot.ActualDebitTotal = voucher.Entries.Sum(e => e.FDebitAmount);
            snapshot.ActualCreditTotal = voucher.Entries.Sum(e => e.FCreditAmount);
            snapshot.ActualBalanced = Math.Abs(snapshot.ActualDebitTotal.Value - snapshot.ActualCreditTotal.Value) <= 0.001m;
        }
    }

    private static void BuildFindings(ValidationBatchContext context, VoucherExplainBatchResult result)
    {
        var rowSnapshots = result.Rows.Values.Where(IsRowAggregation).ToList();

        // 草案有输出但实际凭证缺失（批次执行中属正常推进，不归因写入链路）
        if (!context.IsBatchRunning)
        {
            var missing = rowSnapshots
                .Where(s => s.Matched
                    && s.DraftEntries.Count > 0
                    && s.DraftBalanced == true
                    && !string.IsNullOrWhiteSpace(s.BusinessKey)
                    && s.ActualVoucherId == null)
                .ToList();

            if (missing.Count > 0)
            {
                var sample = missing[0];
                result.Findings.Add(new ImportValidationFindingDto
                {
                    Domain = ValidationDomain.Voucher,
                    Attribution = ValidationAttribution.Persistence,
                    Severity = ValidationSeverity.High,
                    Confidence = 0.8m,
                    AffectedRows = missing.Count,
                    SourceRowId = sample.SourceRowId,
                    BusinessKey = sample.BusinessKey,
                    Title = "凭证草案正常但实际凭证缺失",
                    Message = $"抽样中 {missing.Count} 行按当前规则可生成借贷平衡的凭证（ROW 模式），但按业务键未找到对应 FIN凭证。样例源行 {sample.SourceRowId}（业务键 {sample.BusinessKey}）。",
                    SuggestedAction = "检查自动凭证节点是否执行成功、unmatchedAction 配置与凭证写入链路；必要时重跑自动凭证。",
                    Evidence = BuildRowEvidence(sample)
                });
            }
        }

        // 草案与实际凭证金额不一致 / 实际凭证借贷不平
        var mismatched = rowSnapshots
            .Where(s => s.ActualVoucherId != null
                && s.DraftBalanced == true
                && s.ActualDebitTotal.HasValue
                && (s.ActualBalanced == false
                    || Math.Abs(s.ActualDebitTotal.Value - s.DraftDebitTotal) > context.Tolerance))
            .ToList();

        if (mismatched.Count > 0)
        {
            var sample = mismatched[0];
            var detail = sample.ActualBalanced == false
                ? $"草案借贷平衡（借 {sample.DraftDebitTotal}），实际凭证 {sample.ActualVoucherNo} 借 {sample.ActualDebitTotal} / 贷 {sample.ActualCreditTotal} 不平"
                : $"草案借方合计 {sample.DraftDebitTotal} 与实际凭证 {sample.ActualVoucherNo} 借方合计 {sample.ActualDebitTotal} 差异超容差 {context.Tolerance}";

            result.Findings.Add(new ImportValidationFindingDto
            {
                Domain = ValidationDomain.Voucher,
                Attribution = ValidationAttribution.CalculationLogic,
                Severity = ValidationSeverity.High,
                Confidence = 0.7m,
                AffectedRows = mismatched.Count,
                SourceRowId = sample.SourceRowId,
                BusinessKey = sample.BusinessKey,
                Title = "凭证解释草案与实际凭证不一致",
                Message = $"抽样中 {mismatched.Count} 行的凭证草案与实际凭证存在差异。样例：{detail}。可能为计算逻辑问题，也可能是生成后规则配置已变更。",
                SystemValue = sample.ActualDebitTotal,
                ExpectedValue = sample.DraftDebitTotal,
                Difference = sample.ActualDebitTotal - sample.DraftDebitTotal,
                SuggestedAction = "核对规则组分录配置（金额字段/条件行）近期是否调整；未调整则提交开发按草案与凭证分录核查生成逻辑。",
                Evidence = BuildRowEvidence(sample)
            });
        }

        // 金额字段为空/非数字（导入数据问题）
        var badAmountRows = result.Rows.Values
            .Where(s => s.Matched && s.DraftEntries.Any(e => e.Issue != null && e.Amount == null))
            .ToList();

        if (badAmountRows.Count > 0)
        {
            var sample = badAmountRows[0];
            var sampleEntry = sample.DraftEntries.First(e => e.Issue != null && e.Amount == null);
            result.Findings.Add(new ImportValidationFindingDto
            {
                Domain = ValidationDomain.Voucher,
                Attribution = ValidationAttribution.ImportData,
                Severity = ValidationSeverity.High,
                Confidence = 0.9m,
                AffectedRows = badAmountRows.Count,
                SourceRowId = sample.SourceRowId,
                Title = "凭证金额字段无法解析",
                Message = $"抽样中 {badAmountRows.Count} 行的凭证金额字段无法取数。样例源行 {sample.SourceRowId}：{sampleEntry.Issue}",
                SuggestedAction = "检查导入文件对应列的数据格式（应为数字），或核对规则分录行的金额字段映射。",
                Evidence = BuildRowEvidence(sample)
            });
        }

        // 科目未解析（配置问题：固定/动态/兜底三级均未命中）
        var noAccountRows = result.Rows.Values
            .Where(s => s.Matched && s.DraftEntries.Any(e => e.AccountId == null))
            .ToList();

        if (noAccountRows.Count > 0)
        {
            var sample = noAccountRows[0];
            result.Findings.Add(new ImportValidationFindingDto
            {
                Domain = ValidationDomain.Voucher,
                Attribution = ValidationAttribution.Configuration,
                Severity = ValidationSeverity.High,
                Confidence = 0.85m,
                AffectedRows = noAccountRows.Count,
                SourceRowId = sample.SourceRowId,
                Title = "凭证分录科目未解析",
                Message = $"抽样中 {noAccountRows.Count} 行存在科目未命中（固定/动态匹配/兜底均无结果）。样例源行 {sample.SourceRowId}，规则组[{sample.RuleGroupName}]。",
                SuggestedAction = "在规则组分录行补充动态科目映射或配置兜底科目。",
                Evidence = BuildRowEvidence(sample)
            });
        }

        // 草案自身借贷不平（规则分录配置导致，ROW 模式必然产生不平凭证并生成失败）
        var draftUnbalanced = rowSnapshots
            .Where(s => s.Matched && s.DraftBalanced == false)
            .ToList();

        if (draftUnbalanced.Count > 0)
        {
            var sample = draftUnbalanced[0];
            result.Findings.Add(new ImportValidationFindingDto
            {
                Domain = ValidationDomain.Voucher,
                Attribution = ValidationAttribution.Configuration,
                Severity = ValidationSeverity.High,
                Confidence = 0.8m,
                AffectedRows = draftUnbalanced.Count,
                SourceRowId = sample.SourceRowId,
                Title = "凭证草案借贷不平",
                Message = $"抽样中 {draftUnbalanced.Count} 行按当前规则生成的单行凭证草案借贷不平（样例源行 {sample.SourceRowId}：借 {sample.DraftDebitTotal} / 贷 {sample.DraftCreditTotal}），生成时将整组失败。",
                SuggestedAction = "检查规则组[" + (sample.RuleGroupName ?? "-") + "]的分录行金额字段与条件行配置，确保借贷两方都能取到金额。",
                Evidence = BuildRowEvidence(sample)
            });
        }
    }

    private static ImportValidationEvidenceDto BuildRowEvidence(VoucherExplainSnapshot snapshot)
    {
        var evidence = new ImportValidationEvidenceDto
        {
            TraceSteps = BuildTraceSteps(snapshot),
            PersistedResult =
            {
                ["sourceRowId"] = snapshot.SourceRowId,
                ["ruleGroup"] = snapshot.RuleGroupName,
                ["businessKey"] = snapshot.BusinessKey,
                ["actualVoucherId"] = snapshot.ActualVoucherId,
                ["actualVoucherNo"] = snapshot.ActualVoucherNo
            }
        };
        evidence.ConfigurationIssues.AddRange(snapshot.Issues);
        return evidence;
    }

    /// <summary>把行级解释转为通用计算轨迹（与价格六步同一展示通道）</summary>
    internal static List<CalculationTraceStepDto> BuildTraceSteps(VoucherExplainSnapshot snapshot)
    {
        var steps = new List<CalculationTraceStepDto>();

        if (!snapshot.PassedFilter)
        {
            steps.Add(new CalculationTraceStepDto
            {
                Step = "voucher-filter",
                Description = "凭证预筛选：该行被 filterConditions 排除，不参与凭证生成。"
            });
            return steps;
        }

        steps.Add(new CalculationTraceStepDto
        {
            Step = "voucher-match",
            Description = snapshot.Matched
                ? $"凭证规则命中：Layer{snapshot.MatchedLayer} → 规则组[{snapshot.RuleGroupName}]（{snapshot.AmountAggregation} 聚合）"
                : snapshot.RoutedButNoOutput
                    ? $"凭证规则路由到[{snapshot.RuleGroupName}]但无可接纳分录行"
                    : "凭证规则未命中（三层匹配均无候选）"
        });

        foreach (var entry in snapshot.DraftEntries)
        {
            steps.Add(new CalculationTraceStepDto
            {
                Step = $"voucher-entry-{entry.LineNo}",
                Description = $"分录{entry.LineNo} {entry.Direction}：{entry.AccountCode ?? entry.AccountId?.ToString() ?? "科目未解析"} {entry.AccountName}"
                    + (string.IsNullOrWhiteSpace(entry.Summary) ? string.Empty : $"，摘要[{entry.Summary}]")
                    + (entry.Issue == null ? string.Empty : $"，问题：{entry.Issue}"),
                OutputValue = entry.Amount,
                Formula = $"金额字段[{entry.AmountField}]"
            });
        }

        if (snapshot.DraftBalanced.HasValue)
        {
            steps.Add(new CalculationTraceStepDto
            {
                Step = "voucher-balance",
                Description = snapshot.DraftBalanced.Value
                    ? $"草案借贷平衡：借 {snapshot.DraftDebitTotal} = 贷 {snapshot.DraftCreditTotal}"
                    : $"草案借贷不平：借 {snapshot.DraftDebitTotal} ≠ 贷 {snapshot.DraftCreditTotal}",
                InputValue = snapshot.DraftDebitTotal,
                OutputValue = snapshot.DraftCreditTotal
            });
        }

        if (snapshot.ActualVoucherId != null)
        {
            steps.Add(new CalculationTraceStepDto
            {
                Step = "voucher-reconcile",
                Description = $"实际凭证 {snapshot.ActualVoucherNo}：借 {snapshot.ActualDebitTotal} / 贷 {snapshot.ActualCreditTotal}"
                    + (snapshot.ActualBalanced == false ? "（借贷不平）" : string.Empty),
                InputValue = snapshot.DraftDebitTotal,
                OutputValue = snapshot.ActualDebitTotal
            });
        }
        else if (!string.IsNullOrWhiteSpace(snapshot.BusinessKey))
        {
            steps.Add(new CalculationTraceStepDto
            {
                Step = "voucher-reconcile",
                Description = $"按业务键 {snapshot.BusinessKey} 未找到对应实际凭证。"
            });
        }

        return steps;
    }
}

/// <summary>验证服务传入的抽样源行</summary>
public class VoucherExplainSourceRow
{
    public long? SourceRowId { get; init; }
    public IReadOnlyDictionary<string, object?> Fields { get; init; } = new Dictionary<string, object?>();
}

public class VoucherExplainBatchResult
{
    /// <summary>流程是否配置了自动凭证节点（false 时验证保持记录级校验）</summary>
    public bool HasConfig { get; set; }
    public Dictionary<long, VoucherExplainSnapshot> Rows { get; } = new();
    public List<ImportValidationFindingDto> Findings { get; } = [];
}

/// <summary>
/// 行级解释快照。结构与 ImportValidationVoucherDetailDto 对齐（直接复用其分录/规则行 DTO），
/// 供验证服务装配三段式人工核验视图：原始字段 → 命中规则配置 → 凭证结果。
/// </summary>
public class VoucherExplainSnapshot
{
    public long? SourceRowId { get; set; }
    public bool PassedFilter { get; set; }
    public bool Matched { get; set; }
    public int? MatchedLayer { get; set; }
    /// <summary>命中原因（哪一层、靠哪个字段值/关键词命中）</summary>
    public string? MatchReason { get; set; }
    public List<int> CandidateLayers { get; set; } = [];
    public bool RoutedButNoOutput { get; set; }
    public string? RuleGroupId { get; set; }
    public string? RuleGroupName { get; set; }
    public string? AmountAggregation { get; set; }
    /// <summary>该行参与凭证生成的关键源字段名值（匹配/金额/业务键/日期/分组字段）</summary>
    public Dictionary<string, object?> SourceFieldValues { get; } = new();
    /// <summary>命中规则组的分录行配置快照（含禁用行）</summary>
    public List<ImportValidationVoucherRuleLineDto> RuleLines { get; } = [];
    public List<ImportValidationVoucherEntryDto> DraftEntries { get; } = [];
    public decimal DraftDebitTotal { get; set; }
    public decimal DraftCreditTotal { get; set; }
    /// <summary>仅 ROW 模式有值（单行独立成凭证才能行级判平）</summary>
    public bool? DraftBalanced { get; set; }
    /// <summary>仅 ROW 模式：按 keyFields 重算的业务键（FIN凭证.F数据作用域ID）</summary>
    public string? BusinessKey { get; set; }
    public long? ActualVoucherId { get; set; }
    public string? ActualVoucherNo { get; set; }
    /// <summary>实际凭证分录明细（按业务键反查）</summary>
    public List<ImportValidationVoucherEntryDto> ActualEntries { get; } = [];
    public decimal? ActualDebitTotal { get; set; }
    public decimal? ActualCreditTotal { get; set; }
    public bool? ActualBalanced { get; set; }
    public List<string> Issues { get; } = [];
}
