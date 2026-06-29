using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Handlers;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/expense-classification")]
public class CfExpenseClassificationController : ControllerBase
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IVoucherService _voucherService;
    private readonly ILogger<CfExpenseClassificationController> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CfExpenseClassificationController(
        STOTOPDbContext dbContext,
        IVoucherService voucherService,
        ILogger<CfExpenseClassificationController> logger)
    {
        _dbContext = dbContext;
        _voucherService = voucherService;
        _logger = logger;
    }

    #region 1. GET /api/cardflow/expense-classification/{batchId}

    /// <summary>获取指定批次的费用分类确认数据</summary>
    [HttpGet("{batchId:long}")]
    public async Task<IActionResult> GetClassification(long batchId)
    {
        // 验证批次存在
        var batch = await _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId);
        if (batch == null)
            return Ok(ApiResult.Fail("批次不存在"));

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        const string tableName = "STG费用支出记录";

        // 查询各状态行数
        var totalRows = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM [{tableName}] WHERE [F批次ID] = @batchId",
            new { batchId });
        var autoProcessedRows = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM [{tableName}] WHERE [F批次ID] = @batchId AND [F凭证生成状态] = 1",
            new { batchId });
        var pendingRows = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM [{tableName}] WHERE [F批次ID] = @batchId AND [F凭证生成状态] = 5",
            new { batchId });

        // 查询待分类确认的行
        var rows = (await connection.QueryAsync(
            $"SELECT [FID], [F费用类别], [F费用摘要], [F成本中心], [F支出金额], [F业务日期], [F申请人] FROM [{tableName}] WHERE [F批次ID] = @batchId AND [F凭证生成状态] = 5",
            new { batchId }))
            .Select(r => (IDictionary<string, object>)r)
            .ToList();

        // 加载 V2 规则配置
        var v2Config = await LoadRuleConfigV2Async();
        if (v2Config?.RuleGroups == null || v2Config.RuleGroups.Count == 0)
        {
            return Ok(ApiResult<object>.Success(new
            {
                batchId,
                totalRows,
                autoProcessedRows,
                pendingRows,
                matched = new List<object>(),
                unmatched = rows.Select(r => new
                {
                    id = Convert.ToInt64(r["FID"]),
                    summary = r.TryGetValue("F费用摘要", out var s) ? s?.ToString() ?? "" : "",
                    amount = r.TryGetValue("F支出金额", out var a) ? Convert.ToDecimal(a ?? 0) : 0m,
                    costCenter = r.TryGetValue("F成本中心", out var cc) ? cc?.ToString() ?? "" : "",
                    applicant = r.TryGetValue("F申请人", out var ap) ? ap?.ToString() : null
                }).ToList(),
                categories = new List<object>()
            }));
        }

        // 初始化 V2 三层级联匹配引擎
        var engine = new AutoVoucherMatchingEngineV2();
        engine.Initialize(v2Config);

        // 构建 GroupId → GroupName 映射
        var groupNameMap = v2Config.RuleGroups.ToDictionary(g => g.Id, g => g.Name);

        // 对每行执行三层匹配（推荐分类）
        var matchedGroups = new Dictionary<string, MatchedCategoryBucket>();
        var unmatchedList = new List<object>();

        foreach (var row in rows)
        {
            var fid = Convert.ToInt64(row["FID"]);
            var summary = row.TryGetValue("F费用摘要", out var s2) ? s2?.ToString() ?? "" : "";
            var costCenter = row.TryGetValue("F成本中心", out var cc2) ? cc2?.ToString() ?? "" : "";
            var amount = row.TryGetValue("F支出金额", out var a2) ? Convert.ToDecimal(a2 ?? 0) : 0m;
            var applicant = row.TryGetValue("F申请人", out var ap2) ? ap2?.ToString() : null;

            var candidates = engine.MatchRowToRuleGroup(row);

            string? bestGroup = null;
            if (candidates.Count > 0)
            {
                // 取最高优先级候选（Layer 最小的，即列表第一个）
                var topCandidate = candidates[0];
                if (groupNameMap.TryGetValue(topCandidate.GroupId, out var groupName))
                {
                    bestGroup = groupName;
                }
            }

            if (bestGroup != null)
            {
                if (!matchedGroups.TryGetValue(bestGroup, out var bucket))
                {
                    // 查找对应 ruleGroup 的 accountName（从 Lines[0].SummaryTemplate 或 Name 取）
                    var grp = v2Config.RuleGroups.FirstOrDefault(g => g.Name == bestGroup);
                    var accountName = grp?.Lines?.FirstOrDefault()?.SummaryTemplate ?? bestGroup;

                    bucket = new MatchedCategoryBucket
                    {
                        Category = bestGroup,
                        AccountName = accountName
                    };
                    matchedGroups[bestGroup] = bucket;
                }
                bucket.RowIds.Add(fid);
                bucket.SampleRows.Add(new { id = fid, summary, amount, costCenter });
            }
            else
            {
                unmatchedList.Add(new { id = fid, summary, amount, costCenter, applicant });
            }
        }

        // 构建 matched 结果（sampleRows 最多保留 5 条）
        var matched = matchedGroups.Values.Select(b => new
        {
            b.Category,
            b.AccountName,
            rowCount = b.RowIds.Count,
            rowIds = b.RowIds,
            sampleRows = b.SampleRows.Take(5)
        }).OrderByDescending(m => m.rowCount).ToList();

        // 构建 categories 列表
        var categories = v2Config.RuleGroups
            .Select(g => new { name = g.Name })
            .Distinct()
            .OrderBy(c => c.name)
            .ToList();

        var result = new
        {
            batchId,
            totalRows,
            autoProcessedRows,
            pendingRows,
            matched,
            unmatched = unmatchedList,
            categories
        };

        return Ok(ApiResult<object>.Success(result));
    }

    private class MatchedCategoryBucket
    {
        public string Category { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public List<long> RowIds { get; set; } = new();
        public List<object> SampleRows { get; set; } = new();
    }

    #endregion

    #region 2. POST /api/cardflow/expense-classification/{batchId}/confirm

    /// <summary>确认费用分类并更新 STG 表</summary>
    [HttpPost("{batchId:long}/confirm")]
    public async Task<IActionResult> ConfirmClassification(long batchId, [FromBody] ConfirmClassificationRequest request)
    {
        if (request?.Classifications == null || request.Classifications.Count == 0)
            return Ok(ApiResult.Fail("classifications 不能为空"));

        // 验证批次存在
        var batch = await _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId);
        if (batch == null)
            return Ok(ApiResult.Fail("批次不存在"));

        // 加载 V2 规则配置以验证 category
        var v2Config = await LoadRuleConfigV2Async();
        if (v2Config == null)
            return Ok(ApiResult.Fail("未找到 FID=15 的规则配置"));

        var validCategories = new HashSet<string>(
            (v2Config.RuleGroups ?? new()).Select(g => g.Name));

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        const string tableName = "STG费用支出记录";
        int totalUpdated = 0;

        foreach (var item in request.Classifications)
        {
            if (item.RowIds == null || item.RowIds.Count == 0) continue;

            if (!validCategories.Contains(item.Category))
                return Ok(ApiResult.Fail($"无效的费用类别: {item.Category}"));

            // 验证 rowIds 属于该批次且状态=5
            var validCount = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM [{tableName}] WHERE [F批次ID] = @batchId AND [F凭证生成状态] = 5 AND [FID] IN @RowIds",
                new { batchId, item.RowIds });

            if (validCount != item.RowIds.Count)
                return Ok(ApiResult.Fail($"部分行不属于该批次或状态不是待分类确认，期望 {item.RowIds.Count} 行，实际 {validCount} 行"));

            // 批量更新
            var updated = await connection.ExecuteAsync(
                $"UPDATE [{tableName}] SET [F费用类别] = @Category, [F凭证生成状态] = 0 WHERE [FID] IN @RowIds AND [F批次ID] = @batchId",
                new { item.Category, item.RowIds, batchId });
            totalUpdated += updated;
        }

        // 查询剩余待分类行数
        var remainingPending = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM [{tableName}] WHERE [F批次ID] = @batchId AND [F凭证生成状态] = 5",
            new { batchId });

        return Ok(ApiResult<object>.Success(new
        {
            success = true,
            updatedRows = totalUpdated,
            remainingPendingRows = remainingPending
        }));
    }

    #endregion

    #region 3. POST /api/cardflow/expense-classification/{batchId}/generate-voucher

    /// <summary>对已确认分类的行触发凭证生成</summary>
    [HttpPost("{batchId:long}/generate-voucher")]
    public async Task<IActionResult> GenerateVoucher(long batchId)
    {
        // 验证批次存在
        var batch = await _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId);
        if (batch == null)
            return Ok(ApiResult.Fail("批次不存在"));

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        const string tableName = "STG费用支出记录";

        // Soft check: 是否还有待分类行
        var pendingClassification = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM [{tableName}] WHERE [F批次ID] = @batchId AND [F凭证生成状态] = 5",
            new { batchId });

        string? warning = null;
        if (pendingClassification > 0)
            warning = $"仍有 {pendingClassification} 行待分类确认，本次仅处理已确认的行";

        // 加载 V2 规则配置
        var v2Config = await LoadRuleConfigV2Async();
        if (v2Config == null)
            return Ok(ApiResult.Fail("未找到 FID=15 的规则配置"));

        var ruleGroups = v2Config.RuleGroups ?? new List<RuleGroupV2>();

        // 查询 F凭证生成状态=0 的行（已确认分类待生成凭证）
        var rows = (await connection.QueryAsync(
            $"SELECT * FROM [{tableName}] WHERE [F批次ID] = @batchId AND [F凭证生成状态] = 0",
            new { batchId }))
            .Select(r => (IDictionary<string, object>)r)
            .ToList();

        if (rows.Count == 0)
            return Ok(ApiResult.Fail("没有待生成凭证的行（F凭证生成状态=0）"));

        // 确定会计期间
        var defaultAccountSet = await _dbContext.Set<FinAccountSet>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FIsDefault && a.FStatus == 1);
        long accountSetId = defaultAccountSet?.FID ?? 0;
        if (accountSetId == 0)
            return Ok(ApiResult.Fail("未找到默认账套"));
        DateTime? businessDate = null;
        foreach (var row in rows)
        {
            if (row.TryGetValue("F业务日期", out var dateVal) && dateVal != null && dateVal != DBNull.Value)
            {
                if (dateVal is DateTime dt)
                    businessDate = dt;
                else if (DateTime.TryParse(dateVal.ToString(), out var parsed))
                    businessDate = parsed;
                if (businessDate.HasValue) break;
            }
        }

        long? periodId = null;
        if (businessDate.HasValue)
        {
            var period = await _dbContext.Set<FinAccountPeriod>()
                .Where(p => p.FStartDate <= businessDate.Value
                         && p.FEndDate >= businessDate.Value
                         && p.FAccountSetId == accountSetId
                         && p.FStatus == 1)
                .FirstOrDefaultAsync();
            periodId = period?.FID;
        }

        if (!periodId.HasValue)
            return Ok(ApiResult.Fail($"未找到匹配的会计期间（业务日期: {businessDate?.ToString("yyyy-MM-dd") ?? "无"}，账套: {accountSetId}）"));

        // 按 F费用类别 分组，每个类别生成一张凭证
        var grouped = rows.GroupBy(r =>
        {
            var cat = r.TryGetValue("F费用类别", out var c) ? c?.ToString() ?? "" : "";
            return cat;
        }).Where(g => !string.IsNullOrEmpty(g.Key)).ToList();

        int generatedVouchers = 0;
        int processedRows = 0;
        int failedRows = 0;
        var processedFids = new List<long>();

        foreach (var group in grouped)
        {
            var categoryName = group.Key;
            var categoryRows = group.ToList();

            // 查找匹配的 V2 ruleGroup（按 Name）
            var ruleGroup = ruleGroups.FirstOrDefault(g =>
                g.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            if (ruleGroup == null || ruleGroup.Lines == null || ruleGroup.Lines.Count == 0)
            {
                _logger.LogWarning("费用分类确认: 类别 '{Category}' 无对应规则配置，跳过 {Count} 行",
                    categoryName, categoryRows.Count);
                failedRows += categoryRows.Count;
                continue;
            }

            try
            {
                // 构建凭证分录
                var entries = new List<CreateVoucherEntryRequest>();
                var activeFids = new List<long>();

                // V2: AmountAggregation 在规则组级别
                bool isSumMode = "SUM".Equals(ruleGroup.AmountAggregation, StringComparison.OrdinalIgnoreCase);

                foreach (var line in ruleGroup.Lines.Where(l => l.Status == 1))
                {
                    if (isSumMode)
                    {
                        // SUM 汇总模式
                        decimal totalAmount = 0;
                        foreach (var row in categoryRows)
                        {
                            if (!MatchesLineCondition(row, line)) continue;

                            var amount = row.TryGetValue(line.AmountField, out var av)
                                ? Convert.ToDecimal(av ?? 0) : 0m;
                            totalAmount += amount;

                            if (row.TryGetValue("FID", out var fidVal) && fidVal != null)
                                activeFids.Add(Convert.ToInt64(fidVal));
                        }

                        if (totalAmount == 0) continue;

                        long? lineAccountId = ResolveAccountId(line, categoryRows.First());
                        if (!lineAccountId.HasValue) continue;

                        var summary = ResolveSummary(line.SummaryTemplate, categoryRows.First(), batchId);
                        if (categoryRows.Count > 1)
                            summary += $"（共{categoryRows.Count}行汇总）";

                        entries.Add(new CreateVoucherEntryRequest
                        {
                            Summary = summary,
                            AccountId = lineAccountId.Value,
                            DebitAmount = "借".Equals(line.Direction) ? totalAmount : 0,
                            CreditAmount = "贷".Equals(line.Direction) ? totalAmount : 0
                        });
                    }
                    else
                    {
                        // ROW 逐行模式
                        foreach (var row in categoryRows)
                        {
                            if (!MatchesLineCondition(row, line)) continue;

                            var amount = row.TryGetValue(line.AmountField, out var av)
                                ? Convert.ToDecimal(av ?? 0) : 0m;
                            if (amount == 0) continue;

                            long? lineAccountId = ResolveAccountId(line, row);
                            if (!lineAccountId.HasValue) continue;

                            var summary = ResolveSummary(line.SummaryTemplate, row, batchId);

                            if (row.TryGetValue("FID", out var fidVal) && fidVal != null)
                                activeFids.Add(Convert.ToInt64(fidVal));

                            entries.Add(new CreateVoucherEntryRequest
                            {
                                Summary = summary,
                                AccountId = lineAccountId.Value,
                                DebitAmount = "借".Equals(line.Direction) ? amount : 0,
                                CreditAmount = "贷".Equals(line.Direction) ? amount : 0
                            });
                        }
                    }
                }

                if (entries.Count == 0)
                {
                    failedRows += categoryRows.Count;
                    continue;
                }

                // 分配行号
                for (int i = 0; i < entries.Count; i++)
                    entries[i].LineNo = i + 1;

                // 创建凭证
                var voucherRequest = new CreateVoucherRequest
                {
                    VoucherWord = v2Config.VoucherWord ?? "记",
                    Date = businessDate ?? DateTime.Now,
                    PeriodId = periodId.Value,
                    AttachmentCount = 0,
                    Remark = $"费用分类确认生成 - 批次{batchId} - {categoryName}",
                    Entries = entries
                };

                var voucher = await _voucherService.CreateAsync(voucherRequest, "费用分类确认", accountSetId);
                generatedVouchers++;
                processedRows += activeFids.Distinct().Count();
                processedFids.AddRange(activeFids.Distinct());

                _logger.LogInformation("费用分类确认: 类别 '{Category}' 生成凭证 {VoucherId}，分录 {EntryCount} 条",
                    categoryName, voucher.Id, entries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "费用分类确认: 类别 '{Category}' 凭证生成失败", categoryName);
                failedRows += categoryRows.Count;
            }
        }

        // 批量更新已处理行的状态
        if (processedFids.Count > 0)
        {
            var distinctFids = processedFids.Distinct().ToList();
            await connection.ExecuteAsync(
                $"UPDATE [{tableName}] SET [F凭证生成状态] = 1 WHERE [FID] IN @Fids",
                new { Fids = distinctFids });
        }

        var message = $"成功生成{generatedVouchers}张凭证，处理{processedRows}行";
        if (warning != null)
            message = warning + "。" + message;

        return Ok(ApiResult<object>.Success(new
        {
            success = true,
            generatedVouchers,
            processedRows,
            failedRows,
            message
        }));
    }

    #endregion

    #region Helpers

    /// <summary>加载 FID=15 的 V2 规则配置</summary>
    private async Task<RulesBasedVoucherConfigV2?> LoadRuleConfigV2Async()
    {
        var rule = await _dbContext.Set<CfPluginRule>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FID == 15);

        if (rule?.F规则配置JSON == null) return null;

        try
        {
            var root = JsonDocument.Parse(rule.F规则配置JSON).RootElement;

            // 尝试从 handlerConfig 字段获取
            string configJson;
            if (root.TryGetProperty("handlerConfig", out var hc))
            {
                configJson = hc.ValueKind == JsonValueKind.String ? hc.GetString()! : hc.GetRawText();
            }
            else
            {
                configJson = rule.F规则配置JSON;
            }

            // 解析 V2 格式：顶层含 ruleConfig
            var configRoot = JsonDocument.Parse(configJson).RootElement;
            if (configRoot.TryGetProperty("ruleConfig", out var ruleConfigElement))
            {
                return JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(
                    ruleConfigElement.GetRawText(), JsonOptions);
            }

            _logger.LogWarning("规则 FID=15 不是 V2 格式，无法加载");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析 FID=15 规则配置失败");
            return null;
        }
    }

    private static long? ResolveAccountId(EntryLineV2 line, IDictionary<string, object> row)
    {
        // 固定科目
        if (line.AccountId.HasValue && line.AccountId.Value > 0)
            return line.AccountId.Value;

        // 动态匹配
        if (line.AccountMatchRules != null && !string.IsNullOrEmpty(line.AccountMatchField))
        {
            var fieldValue = row.TryGetValue(line.AccountMatchField, out var fv) ? fv?.ToString() ?? "" : "";
            foreach (var matchRule in line.AccountMatchRules)
            {
                if (!string.IsNullOrEmpty(matchRule.MatchValue)
                    && fieldValue.Contains(matchRule.MatchValue, StringComparison.OrdinalIgnoreCase))
                {
                    return matchRule.AccountId;
                }
            }
        }

        // 兜底科目
        if (line.DefaultAccountId.HasValue && line.DefaultAccountId.Value > 0)
            return line.DefaultAccountId.Value;

        return null;
    }

    private static bool MatchesLineCondition(IDictionary<string, object> row, EntryLineV2 line)
    {
        if (string.IsNullOrEmpty(line.ConditionField) || line.ConditionValues == null || line.ConditionValues.Count == 0)
            return true;

        var fieldValue = row.TryGetValue(line.ConditionField, out var v) ? v?.ToString() ?? "" : "";
        return line.ConditionValues.Any(cv => cv.Equals(fieldValue, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveSummary(string? template, IDictionary<string, object> row, long batchId)
    {
        if (string.IsNullOrEmpty(template)) return "费用分类确认";

        var result = template;
        result = result.Replace("{BatchId}", batchId.ToString());

        // 替换 {F字段名} 占位符
        foreach (var kv in row)
        {
            result = result.Replace($"{{{kv.Key}}}", kv.Value?.ToString() ?? "");
        }

        return result;
    }

    #endregion
}

#region Request / Response DTOs

public class ConfirmClassificationRequest
{
    public List<ClassificationConfirmItem> Classifications { get; set; } = new();
}

public class ClassificationConfirmItem
{
    public List<long> RowIds { get; set; } = new();
    public string Category { get; set; } = string.Empty;
}

#endregion
