using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Dapper;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Handlers;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>
/// AutoVoucher V2 Controller - 字段分析、DryRun、规则 CRUD 和校验 API
/// </summary>
[Authorize]
[ApiController]
[Route("api/cardflow/auto-voucher")]
public class CfAutoVoucherController : ControllerBase
{
    private readonly STOTOPDbContext _context;
    private readonly AutoVoucherMatchingEngineV2 _matchingEngine = new();
    private readonly ILogger<CfAutoVoucherController> _logger;

    public CfAutoVoucherController(
        STOTOPDbContext context,
        ILogger<CfAutoVoucherController> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region 1. 批次字段值分析

    /// <summary>
    /// 批次字段值分析（GET 简版，无 ruleConfigOverride）
    /// </summary>
    [HttpGet("field-analysis/{batchId:long}")]
    public async Task<IActionResult> FieldAnalysisGet(
        long batchId,
        [FromQuery] long? ruleId,
        CancellationToken ct)
    {
        if (ruleId is null)
            return BadRequest(ApiResult.Fail("ruleId 参数必填"));

        return await ExecuteFieldAnalysisAsync(batchId, ruleId.Value, ruleConfigOverride: null, ct);
    }

    /// <summary>
    /// 批次字段值分析（POST 完整版，支持 ruleConfigOverride 或 configJson）
    /// </summary>
    [HttpPost("field-analysis/{batchId:long}")]
    public async Task<IActionResult> FieldAnalysisPost(
        long batchId,
        [FromBody] FieldAnalysisRequest request,
        CancellationToken ct)
    {
        // 优先从 ConfigJson 反序列化
        RulesBasedVoucherConfigV2? configOverride = request.RuleConfigOverride;
        if (!string.IsNullOrWhiteSpace(request.ConfigJson))
        {
            try
            {
                configOverride = JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(request.ConfigJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                return BadRequest(ApiResult.Fail($"configJson 反序列化失败: {ex.Message}"));
            }
        }

        if (request.RuleId <= 0 && configOverride == null)
            return BadRequest(ApiResult.Fail("ruleId 必须大于0，或提供 configJson / ruleConfigOverride"));

        return await ExecuteFieldAnalysisAsync(batchId, request.RuleId, configOverride, ct);
    }

    private async Task<IActionResult> ExecuteFieldAnalysisAsync(
        long batchId, long ruleId, RulesBasedVoucherConfigV2? ruleConfigOverride, CancellationToken ct)
    {
        // 1. 加载规则配置
        var config = await ResolveConfigAsync(ruleId, ruleConfigOverride);
        if (config == null)
            return NotFound(ApiResult.Fail($"规则不存在: ruleId={ruleId}"));

        // 2. 加载批次 → 确定暂存表
        var batch = await _context.Set<CfBatch>().FindAsync(new object[] { batchId }, ct);
        if (batch == null)
            return NotFound(ApiResult.Fail($"批次不存在: batchId={batchId}"));

        var stagingTable = batch.FActualTargetTable ?? config.StagingTable;
        if (string.IsNullOrWhiteSpace(stagingTable))
            return BadRequest(ApiResult.Fail("无法确定暂存表（批次无 FActualTargetTable 且规则无 StagingTable）"));

        // 3. 加载 STG 数据
        var allRows = await LoadStagingDataAsync(stagingTable, batchId, ct);
        if (allRows.Count == 0)
            return Ok(ApiResult<FieldAnalysisResult>.Success(new FieldAnalysisResult
            {
                BatchId = batchId, TotalRows = 0, FilteredTotalRows = 0,
                FilterExcludedRows = 0, MatchingLayers = new MatchingLayerConfig(),
                Layers = new List<FieldAnalysisLayer>(), Summary = new FieldAnalysisSummary()
            }));

        // 4. [G3] 应用 FilterConditions
        var filteredRows = _matchingEngine.ApplyFilterConditions(allRows, config.FilterConditions);
        var filteredTotalRows = filteredRows.Count;
        var filterExcludedRows = allRows.Count - filteredTotalRows;

        // 5. 初始化匹配引擎
        _matchingEngine.Initialize(config);

        // 6. 计算各层分析
        var layers = ComputeFieldAnalysisLayers(filteredRows, config);

        // 7. 计算覆盖率 + 收集未匹配行样本
        int matchedRows = 0;
        var unmatchedRowSample = new List<Dictionary<string, object?>>();
        foreach (var row in filteredRows)
        {
            var candidates = _matchingEngine.MatchRowToRuleGroup(row);
            if (candidates.Count > 0)
            {
                var (groupId, routedButNoOutput) = _matchingEngine.ResolveFinalGroup(row, candidates);
                if (groupId != null && !routedButNoOutput)
                {
                    matchedRows++;
                    continue;
                }
            }
            // 未匹配行，收集前5行作为样本
            if (unmatchedRowSample.Count < 5)
            {
                unmatchedRowSample.Add(row.ToDictionary(
                    kv => kv.Key,
                    kv => (object?)kv.Value));
            }
        }

        var unmatchedRows = filteredTotalRows - matchedRows;

        var result = new FieldAnalysisResult
        {
            BatchId = batchId,
            TotalRows = allRows.Count,
            FilteredTotalRows = filteredTotalRows,
            FilterExcludedRows = filterExcludedRows,
            MatchingLayers = config.MatchingLayers,
            Layers = layers,
            Summary = new FieldAnalysisSummary
            {
                MatchedRows = matchedRows,
                UnmatchedRows = unmatchedRows,
                CoverageRate = filteredTotalRows > 0 ? Math.Round((double)matchedRows / filteredTotalRows, 4) : 0
            },
            UnmatchedRowSample = unmatchedRowSample
        };

        return Ok(ApiResult<FieldAnalysisResult>.Success(result));
    }

    /// <summary>
    /// 计算三层字段值分析
    /// [J1] layers[].values[].count 为"到达该层时仍未匹配的行中该值出现次数"
    /// [J7] Layer3 不做自动分词，仅返回已配置关键词的匹配统计+未覆盖行摘要样本
    /// </summary>
    private List<FieldAnalysisLayer> ComputeFieldAnalysisLayers(
        List<IDictionary<string, object>> rows,
        RulesBasedVoucherConfigV2 config)
    {
        var layers = new List<FieldAnalysisLayer>();
        var remainingRowIndices = Enumerable.Range(0, rows.Count).ToHashSet();

        // Layer 1: 精确编码
        if (!string.IsNullOrEmpty(config.MatchingLayers.ExactMatchField))
        {
            var fieldName = config.MatchingLayers.ExactMatchField;
            var layerTotalRows = remainingRowIndices.Count;
            var valueCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var valueMatchedGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var idx in remainingRowIndices)
            {
                var val = GetStringValue(rows[idx], fieldName);
                if (val == null) continue;
                var trimmed = val.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (!valueCounts.ContainsKey(trimmed))
                    valueCounts[trimmed] = 0;
                valueCounts[trimmed]++;

                // 检查是否精确匹配到规则组
                var candidates = _matchingEngine.MatchRowToRuleGroup(rows[idx]);
                var l1Candidate = candidates.FirstOrDefault(c => c.Layer == 1);
                if (l1Candidate != null && config.RuleGroups.FirstOrDefault(g => g.Id == l1Candidate.GroupId) is { } group)
                {
                    valueMatchedGroups.TryAdd(trimmed, group.Name);
                }
            }

            var matchedIndices = new HashSet<int>();
            foreach (var idx in remainingRowIndices.ToList())
            {
                var candidates = _matchingEngine.MatchRowToRuleGroup(rows[idx]);
                if (candidates.Count > 0 && candidates[0].Layer == 1)
                    matchedIndices.Add(idx);
            }

            remainingRowIndices.ExceptWith(matchedIndices);

            var allValues = valueCounts
                .OrderByDescending(kv => kv.Value)
                .Take(200)
                .Select(kv => new FieldAnalysisValue
                {
                    Value = kv.Key,
                    Count = kv.Value,
                    Matched = valueMatchedGroups.ContainsKey(kv.Key),
                    MatchedGroup = valueMatchedGroups.GetValueOrDefault(kv.Key)
                }).ToList();

            layers.Add(new FieldAnalysisLayer
            {
                Layer = 1,
                LayerName = "Layer1",
                FieldName = fieldName,
                TotalRows = layerTotalRows,
                CoveredRows = matchedIndices.Count,
                UnmatchedValues = allValues.Where(v => !v.Matched).ToList(),
                MatchedValues = allValues.Where(v => v.Matched).ToList()
            });
        }

        // Layer 2: 分类匹配
        if (!string.IsNullOrEmpty(config.MatchingLayers.CategoryField))
        {
            var fieldName = config.MatchingLayers.CategoryField;
            var layerTotalRows = remainingRowIndices.Count;
            var valueCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var valueMatchedGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var idx in remainingRowIndices)
            {
                var val = GetStringValue(rows[idx], fieldName);
                if (val == null) continue;
                var trimmed = val.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (!valueCounts.ContainsKey(trimmed))
                    valueCounts[trimmed] = 0;
                valueCounts[trimmed]++;

                var candidates = _matchingEngine.MatchRowToRuleGroup(rows[idx]);
                var l2Candidate = candidates.FirstOrDefault(c => c.Layer == 2);
                if (l2Candidate != null && config.RuleGroups.FirstOrDefault(g => g.Id == l2Candidate.GroupId) is { } group)
                {
                    valueMatchedGroups.TryAdd(trimmed, group.Name);
                }
            }

            var matchedIndices = new HashSet<int>();
            foreach (var idx in remainingRowIndices.ToList())
            {
                var candidates = _matchingEngine.MatchRowToRuleGroup(rows[idx]);
                if (candidates.Count > 0 && candidates[0].Layer == 2)
                    matchedIndices.Add(idx);
            }

            remainingRowIndices.ExceptWith(matchedIndices);

            var allValues = valueCounts
                .OrderByDescending(kv => kv.Value)
                .Take(200)
                .Select(kv => new FieldAnalysisValue
                {
                    Value = kv.Key,
                    Count = kv.Value,
                    Matched = valueMatchedGroups.ContainsKey(kv.Key),
                    MatchedGroup = valueMatchedGroups.GetValueOrDefault(kv.Key)
                }).ToList();

            layers.Add(new FieldAnalysisLayer
            {
                Layer = 2,
                LayerName = "Layer2",
                FieldName = fieldName,
                TotalRows = layerTotalRows,
                CoveredRows = matchedIndices.Count,
                UnmatchedValues = allValues.Where(v => !v.Matched).ToList(),
                MatchedValues = allValues.Where(v => v.Matched).ToList()
            });
        }

        // [J7] Layer 3: 摘要匹配 - 不做自动分词，仅返回已配置关键词的匹配统计+未覆盖行摘要样本
        if (!string.IsNullOrEmpty(config.MatchingLayers.SummaryField))
        {
            var fieldName = config.MatchingLayers.SummaryField;
            var layerTotalRows = remainingRowIndices.Count;

            // 统计已配置关键词的命中情况
            var keywordStats = new Dictionary<string, KeywordStat>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in config.RuleGroups)
            {
                if (group.SummaryKeywords == null) continue;
                foreach (var kw in group.SummaryKeywords)
                {
                    var trimmedKw = kw?.Trim();
                    if (string.IsNullOrEmpty(trimmedKw)) continue;
                    if (!keywordStats.ContainsKey(trimmedKw))
                        keywordStats[trimmedKw] = new KeywordStat { Keyword = trimmedKw, MatchedGroup = group.Name };
                }
            }

            foreach (var idx in remainingRowIndices)
            {
                var val = GetStringValue(rows[idx], fieldName);
                if (val == null) continue;
                var trimmed = val.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                foreach (var kw in keywordStats.Keys)
                {
                    if (trimmed.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        keywordStats[kw].Count++;
                }
            }

            // 未覆盖行摘要样本
            var unmatchedSamples = new List<string>();
            var l3MatchedIndices = new HashSet<int>();
            foreach (var idx in remainingRowIndices.ToList())
            {
                var candidates = _matchingEngine.MatchRowToRuleGroup(rows[idx]);
                if (candidates.Count > 0 && candidates[0].Layer == 3)
                {
                    l3MatchedIndices.Add(idx);
                }
                else
                {
                    var val = GetStringValue(rows[idx], fieldName);
                    if (val != null && unmatchedSamples.Count < 50)
                        unmatchedSamples.Add(val.Trim());
                }
            }

            remainingRowIndices.ExceptWith(l3MatchedIndices);

            layers.Add(new FieldAnalysisLayer
            {
                Layer = 3,
                LayerName = "Layer3",
                FieldName = fieldName,
                TotalRows = layerTotalRows,
                CoveredRows = l3MatchedIndices.Count,
                UnmatchedValues = new List<FieldAnalysisValue>(),
                MatchedValues = new List<FieldAnalysisValue>(),
                DistinctKeywords = keywordStats
                    .OrderByDescending(kv => kv.Value.Count)
                    .Select(kv => new KeywordMatchStat
                    {
                        Keyword = kv.Key,
                        Count = kv.Value.Count,
                        MatchedGroup = kv.Value.MatchedGroup
                    }).ToList(),
                UnmatchedSamples = unmatchedSamples
            });
        }

        return layers;
    }

    #endregion

    #region 2. DryRun 试运行

    /// <summary>
    /// DryRun 试运行 - 纯计算不持久化，超过30s返回已处理部分+truncated=true
    /// </summary>
    [HttpPost("dry-run")]
    public async Task<IActionResult> DryRun([FromBody] DryRunV2Request request, CancellationToken ct)
    {
        if (request.BatchId <= 0)
            return BadRequest(ApiResult.Fail("batchId 必须大于0"));
        if (request.RuleId <= 0)
            return BadRequest(ApiResult.Fail("ruleId 必须大于0"));

        var config = await ResolveConfigAsync(request.RuleId, request.RuleConfigOverride);
        if (config == null)
            return NotFound(ApiResult.Fail($"规则不存在: ruleId={request.RuleId}"));

        // 加载批次 → 确定暂存表
        var batch = await _context.Set<CfBatch>().FindAsync(new object[] { request.BatchId }, ct);
        if (batch == null)
            return NotFound(ApiResult.Fail($"批次不存在: batchId={request.BatchId}"));

        var stagingTable = batch.FActualTargetTable ?? config.StagingTable;
        if (string.IsNullOrWhiteSpace(stagingTable))
            return BadRequest(ApiResult.Fail("无法确定暂存表"));

        // 加载 STG 数据
        var allRows = await LoadStagingDataAsync(stagingTable, request.BatchId, ct);
        if (allRows.Count == 0)
            return BadRequest(ApiResult.Fail("暂存表无数据"));

        // [G3] 应用 FilterConditions
        var filteredRows = _matchingEngine.ApplyFilterConditions(allRows, config.FilterConditions);

        // 初始化匹配引擎
        _matchingEngine.Initialize(config);

        var sw = Stopwatch.StartNew();
        var timeoutMs = 30_000;
        var truncated = false;
        var processedRows = 0;

        // 按规则组分组统计
        var groupStats = config.RuleGroups.ToDictionary(
            g => g.Id,
            g => new DryRunRuleGroupStat { GroupId = g.Id, GroupName = g.Name, MatchedRows = 0 });

        var layer1Matched = 0;
        var layer2Matched = 0;
        var layer3Matched = 0;
        var unmatchedCount = 0;
        var routedButNoOutputCount = 0;
        var unmatchedSamples = new List<Dictionary<string, object?>>();
        var totalDebit = 0m;
        var totalCredit = 0m;

        for (int i = 0; i < filteredRows.Count; i++)
        {
            // 超时检查
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                truncated = true;
                break;
            }

            var row = filteredRows[i];
            processedRows++;

            var candidates = _matchingEngine.MatchRowToRuleGroup(row);
            var (groupId, routedButNoOutput) = _matchingEngine.ResolveFinalGroup(row, candidates);

            if (groupId == null)
            {
                unmatchedCount++;
                if (unmatchedSamples.Count < 20)
                {
                    var sample = new Dictionary<string, object?>();
                    foreach (var kv in row.Take(10))
                        sample[kv.Key] = kv.Value?.ToString();
                    unmatchedSamples.Add(sample);
                }
                continue;
            }

            if (routedButNoOutput)
            {
                routedButNoOutputCount++;
                continue;
            }

            var group = config.RuleGroups.FirstOrDefault(g => g.Id == groupId);
            if (group == null) continue;

            if (groupStats.TryGetValue(groupId, out var stat))
                stat.MatchedRows++;

            // 统计命中层级
            var hitLayer = candidates.FirstOrDefault(c => c.GroupId == groupId)?.Layer ?? 0;
            if (hitLayer == 1) layer1Matched++;
            else if (hitLayer == 2) layer2Matched++;
            else if (hitLayer == 3) layer3Matched++;

            // 试算金额（仅 SUM 模式简化计算）
            var assigned = _matchingEngine.AssignRowsToEntryLines(new List<IDictionary<string, object>> { row }, group);
            foreach (var (lineNo, assignedRows) in assigned)
            {
                var line = group.Lines.FirstOrDefault(l => l.LineNo == lineNo);
                if (line == null || string.IsNullOrEmpty(line.AmountField)) continue;

                foreach (var aRow in assignedRows)
                {
                    if (aRow.TryGetValue(line.AmountField, out var amtVal) && decimal.TryParse(amtVal?.ToString(), out var amt))
                    {
                        if (line.Direction == "借") totalDebit += amt;
                        else totalCredit += amt;
                    }
                }
            }
        }

        sw.Stop();

        var result = new DryRunV2Result
        {
            LayerStats = new DryRunLayerStats
            {
                Layer1 = new DryRunLayerStat { MatchedRows = layer1Matched },
                Layer2 = new DryRunLayerStat { MatchedRows = layer2Matched },
                Layer3 = new DryRunLayerStat { MatchedRows = layer3Matched },
                Unmatched = unmatchedCount,
                RoutedButNoOutput = routedButNoOutputCount
            },
            RuleGroupStats = groupStats.Values.ToList(),
            UnmatchedSamples = unmatchedSamples,
            BalanceCheck = new DryRunBalanceCheck
            {
                Balanced = Math.Abs(totalDebit - totalCredit) < 0.01m,
                DebitTotal = totalDebit,
                CreditTotal = totalCredit
            },
            Warnings = new List<string>(),
            Truncated = truncated,
            ProcessedRows = processedRows
        };

        return Ok(ApiResult<DryRunV2Result>.Success(result));
    }

    #endregion

    #region 3. 规则 CRUD

    /// <summary>获取规则详情</summary>
    [HttpGet("rules/{ruleId:long}")]
    public async Task<IActionResult> GetRule(long ruleId, CancellationToken ct)
    {
        var rule = await _context.Set<CfPluginRule>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.FID == ruleId, ct);
        if (rule == null)
            return NotFound(ApiResult.Fail("规则不存在"));

        RulesBasedVoucherConfigV2? configV2 = null;
        if (!string.IsNullOrWhiteSpace(rule.F规则配置JSON))
        {
            try
            {
                configV2 = JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(rule.F规则配置JSON,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "规则 {RuleId} JSON 解析失败", ruleId);
            }
        }

        var result = new AutoVoucherRuleDetail
        {
            RuleId = rule.FID,
            RuleName = rule.F规则名称,
            Config = configV2,
            RawConfigJson = rule.F规则配置JSON,
            Status = rule.F状态,
            LastModifiedAt = rule.F更新时间 ?? rule.F创建时间,
            ConcurrencyStamp = rule.FConcurrencyStamp
        };

        return Ok(ApiResult<AutoVoucherRuleDetail>.Success(result));
    }

    /// <summary>
    /// 更新规则（乐观锁：If-Match header 传递 lastModifiedAt 时间戳）
    /// [H6] 乐观锁通过 If-Match header 实现
    /// </summary>
    [HttpPut("rules/{ruleId:long}")]
    public async Task<IActionResult> UpdateRule(
        long ruleId,
        [FromBody] RulesBasedVoucherConfigV2 config,
        CancellationToken ct)
    {
        var entity = await _context.Set<CfPluginRule>().AsTracking()
            .FirstOrDefaultAsync(r => r.FID == ruleId, ct);
        if (entity == null)
            return NotFound(ApiResult.Fail("规则不存在"));

        // [H6] If-Match 乐观锁检查
        if (Request.Headers.TryGetValue("If-Match", out var ifMatchValues))
        {
            var ifMatch = ifMatchValues.ToString().Trim('"');
            var currentStamp = entity.FConcurrencyStamp;
            if (!string.Equals(ifMatch, currentStamp, StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(409, ApiResult.Fail("规则已被他人修改，请刷新后重新编辑"));
            }
        }

        // 校验配置
        var validation = ValidateConfig(config);
        if (validation.Errors.Count > 0)
        {
            return BadRequest(ApiResult.Fail($"规则配置校验失败：{string.Join("; ", validation.Errors)}"));
        }
        var warnings = validation.Warnings;

        // 序列化并保存
        var configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        entity.F规则配置JSON = configJson;
        entity.F更新时间 = DateTime.Now;
        entity.FConcurrencyStamp = Guid.NewGuid().ToString("N");

        // DB 层安全网（EF Core ConcurrencyToken）
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return StatusCode(409, ApiResult.Fail("规则已被他人修改，请刷新后重新编辑"));
        }

        var result = new AutoVoucherRuleUpdateResult
        {
            Success = true,
            Version = config.Version,
            LastModifiedAt = entity.F更新时间 ?? DateTime.Now,
            ConcurrencyStamp = entity.FConcurrencyStamp,
            Warnings = warnings
        };

        return Ok(ApiResult<AutoVoucherRuleUpdateResult>.Success(result));
    }

    /// <summary>校验规则配置</summary>
    [HttpPost("rules/{ruleId:long}/validate")]
    public async Task<IActionResult> ValidateRule(
        long ruleId,
        [FromBody] RulesBasedVoucherConfigV2 config,
        CancellationToken ct)
    {
        // 检查规则是否存在
        var exists = await _context.Set<CfPluginRule>().AnyAsync(r => r.FID == ruleId, ct);
        if (!exists)
            return NotFound(ApiResult.Fail("规则不存在"));

        var result = ValidateConfig(config);
        return Ok(ApiResult<ConfigValidationResult>.Success(result));
    }

    /// <summary>校验规则配置（无需 ruleId，纯校验）</summary>
    [HttpPost("rules/validate")]
    public IActionResult ValidateConfigOnly([FromBody] RulesBasedVoucherConfigV2 config)
    {
        var result = ValidateConfig(config);
        return Ok(ApiResult<ConfigValidationResult>.Success(result));
    }

    #endregion

    #region 4. 校验规则逻辑 - ValidateConfig

    /// <summary>
    /// 校验规则配置
    /// - [D8] accountSetId 必填
    /// - [D2] keyFields 未配置时强制警告
    /// - [E10] ExactCategories 值唯一性（跨规则组不可重复）
    /// - [G6] ExactCodes 值跨规则组唯一性
    /// - [D5] 同长度关键词冲突检测（警告）
    /// - [G7] ROW 模式下 ConditionField + 同方向多分录行 → 警告
    /// - [J3] 借贷方兆底行不对称警告
    /// - 字段名是否在 STG 表中存在（TODO）
    /// - 科目 ID 有效性（TODO）
    /// </summary>
    private ConfigValidationResult ValidateConfig(RulesBasedVoucherConfigV2 config)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // [D8] accountSetId 必填
        if (!config.AccountSetId.HasValue || config.AccountSetId.Value <= 0)
            errors.Add("[D8] accountSetId 必填且必须大于0");

        // [D2] keyFields 未配置时强制警告
        if (config.KeyFields == null || config.KeyFields.Count == 0)
            warnings.Add("[D2] keyFields 未配置，将导致所有行被去重为1行");

        // 模式检查
        if (!string.Equals(config.Mode, "rulesBased", StringComparison.OrdinalIgnoreCase))
            errors.Add("[强制] mode 必须为 'rulesBased'");

        // 规则组校验
        if (config.RuleGroups == null || config.RuleGroups.Count == 0)
        {
            errors.Add("RuleGroups 不能为空");
            return new ConfigValidationResult { Valid = false, Errors = errors, Warnings = warnings };
        }

        // [E10] ExactCategories 值唯一性（跨规则组不可重复）
        var categoryToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in config.RuleGroups)
        {
            if (group.ExactCategories == null) continue;
            foreach (var cat in group.ExactCategories)
            {
                var trimmed = cat?.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (categoryToGroup.TryGetValue(trimmed, out var existingGroup))
                {
                    errors.Add($"[E10] ExactCategories 值 '{trimmed}' 在规则组 '{existingGroup}' 和 '{group.Name}' 中重复");
                }
                else
                {
                    categoryToGroup[trimmed] = group.Name;
                }
            }
        }

        // [G6] ExactCodes 值跨规则组唯一性
        var codeToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in config.RuleGroups)
        {
            if (group.ExactCodes == null) continue;
            foreach (var code in group.ExactCodes)
            {
                var trimmed = code?.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (codeToGroup.TryGetValue(trimmed, out var existingGroup))
                {
                    warnings.Add($"[G6] ExactCodes 值 '{trimmed}' 在规则组 '{existingGroup}' 和 '{group.Name}' 中重复（多层匹配可通过 Layer2/Layer3 区分）");
                }
                else
                {
                    codeToGroup[trimmed] = group.Name;
                }
            }
        }

        // [D5] 同长度关键词冲突检测
        var keywordByLength = new Dictionary<int, List<(string Keyword, string GroupName)>>();
        foreach (var group in config.RuleGroups)
        {
            var allKeywords = new List<string?>();
            if (group.CategoryKeywords != null) allKeywords.AddRange(group.CategoryKeywords);
            if (group.SummaryKeywords != null) allKeywords.AddRange(group.SummaryKeywords);

            foreach (var kw in allKeywords)
            {
                var trimmed = kw?.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (!keywordByLength.ContainsKey(trimmed.Length))
                    keywordByLength[trimmed.Length] = new List<(string, string)>();
                keywordByLength[trimmed.Length].Add((trimmed, group.Name));
            }
        }

        foreach (var kvp in keywordByLength)
        {
            var grouped = kvp.Value.GroupBy(x => x.Keyword, StringComparer.OrdinalIgnoreCase);
            foreach (var g in grouped)
            {
                if (g.Count() > 1)
                {
                    var groupNames = string.Join(", ", g.Select(x => x.GroupName).Distinct());
                    warnings.Add($"[D5] 同长度关键词冲突: '{g.Key}' 出现在多个规则组 ({groupNames})");
                }
            }
        }

        // 逐规则组校验
        foreach (var group in config.RuleGroups)
        {
            if (string.IsNullOrEmpty(group.Id))
                errors.Add($"规则组 '{group.Name}' 缺少 Id");

            if (string.IsNullOrEmpty(group.Name))
                warnings.Add($"规则组 (Id={group.Id}) 缺少 Name");

            if (group.Lines == null || group.Lines.Count == 0)
            {
                errors.Add($"规则组 '{group.Name}' 没有分录行配置");
                continue;
            }

            // [G7] ROW 模式下 ConditionField + 同方向多分录行 → 警告
            if (string.Equals(group.AmountAggregation, "ROW", StringComparison.OrdinalIgnoreCase))
            {
                var debitConditionLines = group.Lines.Count(l => l.Status == 1 && l.Direction == "借" && !string.IsNullOrEmpty(l.ConditionField));
                var creditConditionLines = group.Lines.Count(l => l.Status == 1 && l.Direction == "贷" && !string.IsNullOrEmpty(l.ConditionField));
                var debitTotalLines = group.Lines.Count(l => l.Status == 1 && l.Direction == "借");
                var creditTotalLines = group.Lines.Count(l => l.Status == 1 && l.Direction == "贷");

                if (debitConditionLines > 0 && debitTotalLines > 1)
                    warnings.Add($"[G7] 规则组 '{group.Name}' ROW 模式下借方有 ConditionField + 多分录行，可能导致分配不均");

                if (creditConditionLines > 0 && creditTotalLines > 1)
                    warnings.Add($"[G7] 规则组 '{group.Name}' ROW 模式下贷方有 ConditionField + 多分录行，可能导致分配不均");
            }

            // [J3] 借贷方兆底行不对称警告
            var debitCatchall = group.Lines.Count(l => l.Status == 1 && l.Direction == "借" && string.IsNullOrEmpty(l.ConditionField));
            var creditCatchall = group.Lines.Count(l => l.Status == 1 && l.Direction == "贷" && string.IsNullOrEmpty(l.ConditionField));

            if (debitCatchall != creditCatchall)
                warnings.Add($"[J3] 规则组 '{group.Name}' 借贷方兆底行不对称：借方 {debitCatchall} 行，贷方 {creditCatchall} 行");

            // 分录行校验
            foreach (var line in group.Lines.Where(l => l.Status == 1))
            {
                if (string.IsNullOrEmpty(line.AmountField))
                    errors.Add($"规则组 '{group.Name}' 分录行 {line.LineNo} 缺少 AmountField");

                // 科目有效性
                if (!line.AccountId.HasValue && string.IsNullOrEmpty(line.AccountMatchField) && !line.DefaultAccountId.HasValue)
                    warnings.Add($"规则组 '{group.Name}' 分录行 {line.LineNo} 无科目配置（AccountId/AccountMatchField/DefaultAccountId 均为空）");
            }
        }

        // MatchingLayers 校验
        if (string.IsNullOrEmpty(config.MatchingLayers.ExactMatchField) &&
            string.IsNullOrEmpty(config.MatchingLayers.CategoryField) &&
            string.IsNullOrEmpty(config.MatchingLayers.SummaryField))
        {
            errors.Add("MatchingLayers 至少需要配置一个匹配字段");
        }

        // TODO: 字段名是否在 STG 表中存在
        warnings.Add("[TODO] 字段名是否在 STG 表中存在 - 待实现");

        // TODO: 科目 ID 有效性
        warnings.Add("[TODO] 科目 ID 有效性校验 - 待实现");

        return new ConfigValidationResult
        {
            Valid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    #endregion

    #region 私有辅助方法

    /// <summary>解析规则配置（支持 ruleConfigOverride 覆盖）</summary>
    private async Task<RulesBasedVoucherConfigV2?> ResolveConfigAsync(
        long ruleId, RulesBasedVoucherConfigV2? ruleConfigOverride)
    {
        if (ruleConfigOverride != null)
            return ruleConfigOverride;

        var rule = await _context.Set<CfPluginRule>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.FID == ruleId);
        if (rule == null)
            return null;

        if (string.IsNullOrWhiteSpace(rule.F规则配置JSON))
            return null;

        try
        {
            return JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(rule.F规则配置JSON,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "规则 {RuleId} JSON 反序列化失败", ruleId);
            return null;
        }
    }

    /// <summary>加载暂存表数据</summary>
    private async Task<List<IDictionary<string, object>>> LoadStagingDataAsync(
        string stagingTable, long batchId, CancellationToken ct)
    {
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        var sql = $"SELECT * FROM [{stagingTable}] WHERE [F批次ID] = @batchId ORDER BY [F原始行号]";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var param = cmd.CreateParameter();
        param.ParameterName = "@batchId";
        param.Value = batchId;
        cmd.Parameters.Add(param);

        if (ct.IsCancellationRequested) return new List<IDictionary<string, object>>();

        var items = new List<IDictionary<string, object>>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var dict = new Dictionary<string, object>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
            }
            items.Add(dict);
        }

        return items;
    }

    /// <summary>安全获取字符串值</summary>
    private static string? GetStringValue(IDictionary<string, object> row, string fieldName)
    {
        if (!row.TryGetValue(fieldName, out var raw) || raw is null)
            return null;
        return raw.ToString()?.Trim();
    }

    #endregion
}

#region 请求/响应 DTOs

/// <summary>字段分析请求</summary>
public class FieldAnalysisRequest
{
    public long RuleId { get; set; } = 0;
    public string? ConfigJson { get; set; }
    public RulesBasedVoucherConfigV2? RuleConfigOverride { get; set; }
}

/// <summary>字段分析结果</summary>
public class FieldAnalysisResult
{
    public long BatchId { get; set; }
    public int TotalRows { get; set; }
    public int FilteredTotalRows { get; set; }
    public int FilterExcludedRows { get; set; }
    public MatchingLayerConfig MatchingLayers { get; set; } = new();
    public List<FieldAnalysisLayer> Layers { get; set; } = new();
    public FieldAnalysisSummary Summary { get; set; } = new();
    public List<Dictionary<string, object?>> UnmatchedRowSample { get; set; } = new();
}

/// <summary>层级分析数据</summary>
public class FieldAnalysisLayer
{
    public int Layer { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public int TotalRows { get; set; }
    public int CoveredRows { get; set; }
    public List<FieldAnalysisValue> UnmatchedValues { get; set; } = new();
    public List<FieldAnalysisValue> MatchedValues { get; set; } = new();
    /// <summary>Layer3 的已配置关键词匹配统计</summary>
    public List<KeywordMatchStat>? DistinctKeywords { get; set; }
    /// <summary>Layer3 未覆盖行摘要样本</summary>
    public List<string>? UnmatchedSamples { get; set; }
}

/// <summary>值分布条目</summary>
public class FieldAnalysisValue
{
    public string Value { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool Matched { get; set; }
    public string? MatchedGroup { get; set; }
}

/// <summary>关键词匹配统计</summary>
public class KeywordMatchStat
{
    public string Keyword { get; set; } = string.Empty;
    public int Count { get; set; }
    public string? MatchedGroup { get; set; }
}

/// <summary>字段分析汇总</summary>
public class FieldAnalysisSummary
{
    public int MatchedRows { get; set; }
    public int UnmatchedRows { get; set; }
    public double CoverageRate { get; set; }
}

/// <summary>DryRun V2 请求</summary>
public class DryRunV2Request
{
    public long RuleId { get; set; }
    public long BatchId { get; set; }
    public RulesBasedVoucherConfigV2? RuleConfigOverride { get; set; }
}

/// <summary>DryRun V2 结果</summary>
public class DryRunV2Result
{
    public DryRunLayerStats LayerStats { get; set; } = new();
    public List<DryRunRuleGroupStat> RuleGroupStats { get; set; } = new();
    public List<Dictionary<string, object?>> UnmatchedSamples { get; set; } = new();
    public DryRunBalanceCheck BalanceCheck { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool Truncated { get; set; }
    public int ProcessedRows { get; set; }
}

/// <summary>DryRun 层级统计</summary>
public class DryRunLayerStats
{
    public DryRunLayerStat Layer1 { get; set; } = new();
    public DryRunLayerStat Layer2 { get; set; } = new();
    public DryRunLayerStat Layer3 { get; set; } = new();
    public int Unmatched { get; set; }
    public int RoutedButNoOutput { get; set; }
}

/// <summary>DryRun 单层统计</summary>
public class DryRunLayerStat
{
    public int MatchedRows { get; set; }
}

/// <summary>DryRun 规则组统计</summary>
public class DryRunRuleGroupStat
{
    public string GroupId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int MatchedRows { get; set; }
}

/// <summary>DryRun 借贷平衡检查</summary>
public class DryRunBalanceCheck
{
    public bool Balanced { get; set; }
    public decimal DebitTotal { get; set; }
    public decimal CreditTotal { get; set; }
}

/// <summary>规则详情</summary>
public class AutoVoucherRuleDetail
{
    public long RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public RulesBasedVoucherConfigV2? Config { get; set; }
    public string? RawConfigJson { get; set; }
    public int Status { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

/// <summary>规则更新结果</summary>
public class AutoVoucherRuleUpdateResult
{
    public bool Success { get; set; }
    public int Version { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public string? ConcurrencyStamp { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>配置校验结果</summary>
public class ConfigValidationResult
{
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>关键词统计内部结构</summary>
internal class KeywordStat
{
    public string Keyword { get; set; } = string.Empty;
    public int Count { get; set; }
    public string? MatchedGroup { get; set; }
}

#endregion
