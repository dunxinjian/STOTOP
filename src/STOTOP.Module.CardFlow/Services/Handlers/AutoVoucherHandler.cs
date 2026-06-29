using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Auxiliary;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.CardFlow.AutoPlugin;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("STOTOP.Module.CardFlow.Tests")]

namespace STOTOP.Module.CardFlow.Services.Handlers;

public partial class AutoVoucherHandler : IClassificationHandler
{
    private readonly IVoucherService _voucherService;
    private readonly STOTOPDbContext _dbContext;
    private readonly IProgressNotifier _notifier;
    private readonly ILogger<AutoVoucherHandler> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // 合法表名：以 STG 开头，后跟中文/字母/数字/下划线
    [GeneratedRegex(@"^STG[\u4e00-\u9fa5A-Za-z0-9_]+$")]
    private static partial Regex ValidTableNameRegex();

    public string HandlerType => "AutoVoucher";

    public AutoVoucherHandler(
        IVoucherService voucherService,
        STOTOPDbContext dbContext,
        IProgressNotifier notifier,
        ILogger<AutoVoucherHandler> logger)
    {
        _voucherService = voucherService;
        _dbContext = dbContext;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<HandlerResult> HandleAsync(HandlerContext context)
    {
        if (string.IsNullOrWhiteSpace(context.HandlerConfig))
            return HandlerResult.Fail("AutoVoucherHandler: HandlerConfig 为空，无法执行凭证生成");

        // 防御双重序列化：如果 JSON 以引号开头，先解包一层
        var configJson = context.HandlerConfig.Trim();
        if (configJson.StartsWith('"') && configJson.EndsWith('"'))
        {
            try
            {
                configJson = JsonSerializer.Deserialize<string>(configJson) ?? configJson;
                _logger.LogWarning("AutoVoucherHandler: 检测到双重序列化的 HandlerConfig，已自动解包");
            }
            catch { /* 不是双重序列化，保持原值 */ }
        }

        // V2 统一路径
        var (v2Config, runtimeCtx) = ParseHandlerInput(configJson);
        return await HandleV2Async(v2Config, runtimeCtx);
    }

    #region V2 模式

    /// <summary>
    /// [G1] 解析 Handler 输入配置（V2 统一格式）
    /// </summary>
    private (RulesBasedVoucherConfigV2 config, AutoVoucherRuntimeContext ctx) ParseHandlerInput(string configJson)
    {
        using var doc = JsonDocument.Parse(configJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("ruleConfig", out var ruleConfigElement))
            throw new InvalidOperationException("AutoVoucherHandler: 配置缺少 ruleConfig，请检查规则配置格式");

        var ruleConfig = JsonSerializer.Deserialize<RulesBasedVoucherConfigV2>(
            ruleConfigElement.GetRawText(), _jsonOptions)
            ?? throw new InvalidOperationException("AutoVoucherHandler: ruleConfig 反序列化失败");

        var runtimeContext = root.TryGetProperty("runtimeContext", out var rtcElement)
            ? JsonSerializer.Deserialize<AutoVoucherRuntimeContext>(rtcElement.GetRawText(), _jsonOptions)!
            : new AutoVoucherRuntimeContext();

        return (ruleConfig, runtimeContext);
    }

    /// <summary>
    /// V2 凭证生成主流程
    /// [H10] 事务边界：以 GroupBy 组为粒度
    /// </summary>
    private async Task<HandlerResult> HandleV2Async(RulesBasedVoucherConfigV2 config, AutoVoucherRuntimeContext ctx)
    {
        // 1. 配置校验
        if (config.RuleGroups.Count == 0)
            return HandlerResult.Fail("V2配置错误: RuleGroups为空");

        var stagingTable = config.StagingTable;
        if (string.IsNullOrEmpty(stagingTable))
            return HandlerResult.Fail("V2配置错误: StagingTable未配置");

        if (!ValidTableNameRegex().IsMatch(stagingTable))
            return HandlerResult.Fail($"V2配置错误: 非法暂存表名 {stagingTable}");

        long accountSetId = config.AccountSetId ?? 0;
        if (accountSetId == 0)
            return HandlerResult.Fail("V2配置错误: AccountSetId未配置");

        // 2. 加载 STG 表数据行
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        var sql = $"SELECT * FROM [{stagingTable}] WHERE [F批次ID] = @batchId";
        var rows = (await connection.QueryAsync(sql, new { batchId = ctx.BatchId }))
            .Select(r => (IDictionary<string, object>)r)
            .ToList();

        if (rows.Count == 0)
            return HandlerResult.Fail("V2: 暂存表无数据行");

        _logger.LogInformation("AutoVoucher V2: 加载 {Count} 行数据（批次 {BatchId}，表 {Table}）",
            rows.Count, ctx.BatchId, stagingTable);

        // 3. [E5] 预筛选 FilterConditions
        var engine = new AutoVoucherMatchingEngineV2();
        var filteredRows = engine.ApplyFilterConditions(rows, config.FilterConditions);

        if (filteredRows.Count == 0)
            return HandlerResult.Fail("V2: 预筛选后无可处理数据行");

        if (filteredRows.Count < rows.Count)
            _logger.LogInformation("AutoVoucher V2: 预筛选过滤 {Filtered}/{Total} 行",
                rows.Count - filteredRows.Count, rows.Count);

        // 4. GroupBy 分组（GroupBy为空时整批为一组）
        var groups = string.IsNullOrEmpty(config.GroupBy)
            ? new List<List<IDictionary<string, object>>> { filteredRows }
            : filteredRows.GroupBy(r => r.TryGetValue(config.GroupBy, out var gv) ? gv?.ToString() ?? "" : "")
                .Select(g => g.ToList()).ToList();

        // 5. 初始化匹配引擎
        engine.Initialize(config);

        // 6. 预加载辅助核算项目
        var auxResolver = new AutoVoucherAuxiliaryResolver(_logger);
        var auxItems = await _dbContext.Set<FinAuxiliaryItem>()
            .IgnoreQueryFilters() // BUG1: 绕过 IOrgScoped 全局过滤(FOrgId==192)，否则 FOrgId==0 的全局项(express_brand/business_direction)被滤掉，只剩 outlet
            .Where(a => a.FEnableStatus == 1)
            .Where(a => a.FOrgId == ctx.OrgId || a.FOrgId == 0)
            .Select(a => new AuxiliaryItemInfo { Id = a.FID, AuxType = a.FAuxType ?? "", Code = a.FCode, Name = a.FName })
            .ToListAsync();
        auxResolver.Initialize(auxItems);

        // 6b. 预加载本账套科目契约(FID→F辅助核算)，供分录按科目声明维度补齐辅助核算(方案B 源打标 C)
        var accountAuxMap = await _dbContext.Set<FinAccount>()
            .IgnoreQueryFilters() // 科目为账套级共享，绕过 IOrgScoped 历史过滤(若存在)，按账套取
            .Where(a => a.FAccountSetId == accountSetId)
            .Select(a => new { a.FID, a.FAuxiliary })
            .ToDictionaryAsync(a => a.FID, a => a.FAuxiliary);

        // 7. 对每个 GroupBy 组处理
        int successGroups = 0, failedGroups = 0, skippedRows = 0;
        int totalUnmatched = 0;
        var warnings = new List<string>();
        var generatedVoucherIds = new List<long>();
        int processedGroupIndex = 0;
        // [D10] createDraft: 跨所有 GroupBy 组收集全量未匹配行，循环结束后统一生成待补录草稿
        var allUnmatchedRows = new List<IDictionary<string, object>>();

        foreach (var groupRows in groups)
        {
            processedGroupIndex++;
            await _notifier.NotifyAutoPluginDataProgressAsync(ctx.BatchId, "自动凭证V2",
                processedGroupIndex, groups.Count, "处理凭证组");

            try
            {
                // 7a. 逐行匹配 → ResolveFinalGroup
                var rowsByRuleGroup = new Dictionary<string, List<IDictionary<string, object>>>();
                int unmatchedCount = 0;
                int routedButNoOutputCount = 0;
                var unmatchedSampleRows = new List<IDictionary<string, object>>();
                const int maxSampleRows = 3;

                foreach (var row in groupRows)
                {
                    var candidates = engine.MatchRowToRuleGroup(row);
                    var (groupId, routedButNoOutput) = engine.ResolveFinalGroup(row, candidates);

                    if (groupId == null)
                    {
                        unmatchedCount++;
                        allUnmatchedRows.Add(row); // [D10] 全量收集供 createDraft，与采样并存不影响 skip 语义
                        if (unmatchedSampleRows.Count < maxSampleRows)
                            unmatchedSampleRows.Add(row);
                        continue;
                    }
                    if (routedButNoOutput)
                    {
                        routedButNoOutputCount++;
                        continue;
                    }

                    if (!rowsByRuleGroup.ContainsKey(groupId))
                        rowsByRuleGroup[groupId] = new List<IDictionary<string, object>>();
                    rowsByRuleGroup[groupId].Add(row);
                }

                totalUnmatched += unmatchedCount;

                // 记录未匹配明细（无论 UnmatchedAction 为何值都写入）
                if (unmatchedCount > 0)
                {
                    warnings.Add($"组{processedGroupIndex}: {unmatchedCount}行未匹配规则");
                    foreach (var sampleRow in unmatchedSampleRows)
                        warnings.Add(BuildUnmatchedRowDetail(sampleRow, config.MatchingLayers));
                }
                if (routedButNoOutputCount > 0)
                    warnings.Add($"组{processedGroupIndex}: {routedButNoOutputCount}行匹配规则组但无输出（条件不满足）");

                // UnmatchedAction 处理
                if (unmatchedCount > 0 && config.UnmatchedAction == "error")
                {
                    failedGroups++;
                    // 上面已写入未匹配明细，此处仅标记整组跳过
                    continue;
                }

                if (rowsByRuleGroup.Count == 0)
                {
                    skippedRows += groupRows.Count;
                    continue;
                }

                // 7b-c. 按规则组收集行并构建凭证
                if (string.IsNullOrEmpty(config.GroupBy))
                {
                    // ===== 新逻辑：GroupBy为空时按(规则组 + 业务日期)拆分为多张凭证 =====
                    foreach (var (ruleGroupId, matchedRows) in rowsByRuleGroup)
                    {
                        var ruleGroup = config.RuleGroups.FirstOrDefault(g => g.Id == ruleGroupId);
                        if (ruleGroup == null) continue;

                        // 按业务日期再分组
                        var dateGroups = GroupRowsByDateField(matchedRows, config.DateField);

                        foreach (var (dateKey, dateRows) in dateGroups)
                        {
                            var voucherEntries = new List<V2VoucherEntryDto>();

                            if (ruleGroup.AmountAggregation == "ROW")
                            {
                                foreach (var row in dateRows)
                                {
                                    var singleRowList = new List<IDictionary<string, object>> { row };
                                    var assigned = engine.AssignRowsToEntryLines(singleRowList, ruleGroup);
                                    BuildEntryLinesV2(assigned, ruleGroup, row, auxResolver, voucherEntries, warnings, accountAuxMap);
                                }
                            }
                            else // SUM
                            {
                                var assigned = engine.AssignRowsToEntryLines(dateRows, ruleGroup);
                                var firstRow = !string.IsNullOrEmpty(ruleGroup.SumFirstRowOrderBy)
                                    ? dateRows.OrderBy(r => r.TryGetValue(ruleGroup.SumFirstRowOrderBy, out var v) ? v?.ToString() ?? "" : "").First()
                                    : dateRows.First();
                                BuildEntryLinesV2(assigned, ruleGroup, firstRow, auxResolver, voucherEntries, warnings, accountAuxMap);
                            }

                            // [H3] 借贷平衡校验
                            var debitTotal = voucherEntries.Where(e => e.Direction == "借").Sum(e => e.Amount);
                            var creditTotal = voucherEntries.Where(e => e.Direction == "贷").Sum(e => e.Amount);
                            if (Math.Abs(debitTotal - creditTotal) > 0.001m)
                            {
                                failedGroups++;
                                var dateLabel = dateKey?.ToString("yyyy-MM-dd") ?? "无日期";
                                warnings.Add($"组{processedGroupIndex}-规则组{ruleGroupId}-日期{dateLabel}: 借贷不平衡 借方={debitTotal}, 贷方={creditTotal}, 差额={debitTotal - creditTotal}");
                                continue;
                            }

                            if (voucherEntries.Count == 0) continue;

                            // [H2] ComputeBusinessKey 去重检查
                            string? businessKey = ComputeBusinessKeyV2(dateRows, config.KeyFields, ctx.BatchId);
                            if (!string.IsNullOrEmpty(businessKey))
                            {
                                var existingVoucher = await connection.QueryFirstOrDefaultAsync<long?>(
                                    "SELECT TOP 1 FID FROM [FIN凭证] WHERE [F数据作用域ID] = @Key",
                                    new { Key = businessKey });
                                if (existingVoucher.HasValue)
                                {
                                    _logger.LogInformation("AutoVoucher V2: 去重跳过 businessKey={Key}", businessKey);
                                    skippedRows += dateRows.Count;
                                    continue;
                                }
                            }

                            // 写入数据库（创建凭证+分录）
                            var entries = voucherEntries.Select((e, idx) => new CreateVoucherEntryRequest
                            {
                                LineNo = idx + 1,
                                Summary = e.Summary ?? "",
                                AccountId = e.AccountId ?? 0,
                                AuxiliaryJson = e.AuxiliaryJson,
                                DebitAmount = e.Direction == "借" ? e.Amount : 0,
                                CreditAmount = e.Direction == "贷" ? e.Amount : 0
                            }).ToList();

                            // 凭证日期取该组的业务日期
                            DateTime voucherDate = dateKey ?? DateTime.Now;

                            // 通过凭证日期自动查找会计期间
                            var period = await _dbContext.Set<FinAccountPeriod>()
                                .FirstOrDefaultAsync(p => p.FAccountSetId == accountSetId
                                    && p.FStartDate <= voucherDate && p.FEndDate >= voucherDate);
                            if (period == null)
                            {
                                failedGroups++;
                                warnings.Add($"组{processedGroupIndex}-规则组{ruleGroupId}: 凭证日期{voucherDate:yyyy-MM-dd}未找到匹配的会计期间(账套{accountSetId})");
                                continue;
                            }

                            var dateSuffix = dateKey?.ToString("yyyyMMdd") ?? "nodate";
                            var request = new CreateVoucherRequest
                            {
                                VoucherWord = config.VoucherWord,
                                Date = voucherDate,
                                PeriodId = period.FID,
                                AttachmentCount = 0,
                                Remark = $"数据导入自动生成(V2) - 批次{ctx.BatchId}",
                                Source = "数据导入",
                                DataScopeId = businessKey ?? $"{ctx.BatchId}_{ruleGroupId}_{dateSuffix}",
                                Entries = entries
                            };

                            var voucher = await _voucherService.CreateAsync(request, "数据导入系统", accountSetId);
                            generatedVoucherIds.Add(voucher.Id);
                            successGroups++;

                            _logger.LogInformation("AutoVoucher V2: 创建凭证 {VoucherId}, 规则组={RuleGroup}, 日期={Date}, 分录数:{Count}",
                                voucher.Id, ruleGroupId, voucherDate.ToString("yyyy-MM-dd"), entries.Count);
                        }
                    }
                }
                else
                {
                    // ===== 原有逻辑：GroupBy不为空时，所有规则组合并为一张凭证 =====
                    var voucherEntries = new List<V2VoucherEntryDto>();

                    foreach (var (ruleGroupId, matchedRows) in rowsByRuleGroup)
                    {
                        var ruleGroup = config.RuleGroups.FirstOrDefault(g => g.Id == ruleGroupId);
                        if (ruleGroup == null) continue;

                        if (ruleGroup.AmountAggregation == "ROW")
                        {
                            foreach (var row in matchedRows)
                            {
                                var singleRowList = new List<IDictionary<string, object>> { row };
                                var assigned = engine.AssignRowsToEntryLines(singleRowList, ruleGroup);
                                BuildEntryLinesV2(assigned, ruleGroup, row, auxResolver, voucherEntries, warnings, accountAuxMap);
                            }
                        }
                        else // SUM
                        {
                            var assigned = engine.AssignRowsToEntryLines(matchedRows, ruleGroup);
                            var firstRow = !string.IsNullOrEmpty(ruleGroup.SumFirstRowOrderBy)
                                ? matchedRows.OrderBy(r => r.TryGetValue(ruleGroup.SumFirstRowOrderBy, out var v) ? v?.ToString() ?? "" : "").First()
                                : matchedRows.First();
                            BuildEntryLinesV2(assigned, ruleGroup, firstRow, auxResolver, voucherEntries, warnings, accountAuxMap);
                        }
                    }

                    // [H3] 借贷平衡校验
                    var debitTotal = voucherEntries.Where(e => e.Direction == "借").Sum(e => e.Amount);
                    var creditTotal = voucherEntries.Where(e => e.Direction == "贷").Sum(e => e.Amount);
                    if (Math.Abs(debitTotal - creditTotal) > 0.001m)
                    {
                        failedGroups++;
                        warnings.Add($"组{processedGroupIndex}: 借贷不平衡 借方={debitTotal}, 贷方={creditTotal}, 差额={debitTotal - creditTotal}");
                        continue;
                    }

                    if (voucherEntries.Count == 0)
                    {
                        skippedRows += groupRows.Count;
                        continue;
                    }

                    // [H2] ComputeBusinessKey 去重检查
                    string? businessKey = ComputeBusinessKeyV2(groupRows, config.KeyFields, ctx.BatchId);
                    if (!string.IsNullOrEmpty(businessKey))
                    {
                        var existingVoucher = await connection.QueryFirstOrDefaultAsync<long?>(
                            "SELECT TOP 1 FID FROM [FIN凭证] WHERE [F数据作用域ID] = @Key",
                            new { Key = businessKey });
                        if (existingVoucher.HasValue)
                        {
                            _logger.LogInformation("AutoVoucher V2: 去重跳过 businessKey={Key}", businessKey);
                            skippedRows += groupRows.Count;
                            continue;
                        }
                    }

                    // 写入数据库（创建凭证+分录）
                    var entries = voucherEntries.Select((e, idx) => new CreateVoucherEntryRequest
                    {
                        LineNo = idx + 1,
                        Summary = e.Summary ?? "",
                        AccountId = e.AccountId ?? 0,
                        AuxiliaryJson = e.AuxiliaryJson,
                        DebitAmount = e.Direction == "借" ? e.Amount : 0,
                        CreditAmount = e.Direction == "贷" ? e.Amount : 0
                    }).ToList();

                    // 确定凭证日期
                    DateTime voucherDate = DateTime.Now;
                    if (!string.IsNullOrEmpty(config.DateField) && groupRows.Count > 0)
                    {
                        var firstRow = groupRows[0];
                        if (firstRow.TryGetValue(config.DateField, out var dv))
                        {
                            if (dv is DateTime dt) voucherDate = dt;
                            else if (dv != null && DateTime.TryParse(dv.ToString(), out var parsed)) voucherDate = parsed;
                        }
                    }

                    // 通过凭证日期自动查找会计期间
                    var period = await _dbContext.Set<FinAccountPeriod>()
                        .FirstOrDefaultAsync(p => p.FAccountSetId == accountSetId
                            && p.FStartDate <= voucherDate && p.FEndDate >= voucherDate);
                    if (period == null)
                    {
                        failedGroups++;
                        warnings.Add($"组{processedGroupIndex}: 凭证日期{voucherDate:yyyy-MM-dd}未找到匹配的会计期间(账套{accountSetId})");
                        continue;
                    }

                    var request = new CreateVoucherRequest
                    {
                        VoucherWord = config.VoucherWord,
                        Date = voucherDate,
                        PeriodId = period.FID,
                        AttachmentCount = 0,
                        Remark = $"数据导入自动生成(V2) - 批次{ctx.BatchId}",
                        Source = "数据导入",
                        DataScopeId = businessKey ?? ctx.BatchId.ToString(),
                        Entries = entries
                    };

                    var voucher = await _voucherService.CreateAsync(request, "数据导入系统", accountSetId);
                    generatedVoucherIds.Add(voucher.Id);
                    successGroups++;

                    _logger.LogInformation("AutoVoucher V2: 创建凭证 {VoucherId}, 字:{Word}-{No}, 分录数:{Count}",
                        voucher.Id, voucher.VoucherWord, voucher.VoucherNo, entries.Count);
                }
            }
            catch (Exception ex)
            {
                failedGroups++;
                warnings.Add($"组{processedGroupIndex}异常: {ex.Message}");
                _logger.LogError(ex, "AutoVoucher V2: GroupBy 组{Index}处理失败（批次 {BatchId}）",
                    processedGroupIndex, ctx.BatchId);
            }
        }

        // [D10] createDraft: 未匹配行真生待补录草稿（循环之后，按业务日期分组，仿会计期间查询）
        if (config.UnmatchedAction == "createDraft" && allUnmatchedRows.Count > 0)
        {
            // BUG2 防崩: 占位科目无效则跳过草稿生成。F科目ID 不可空 + FK 到 FIN科目，
            // AccountId=0 会触发 FK_FIN凭证分录_科目ID 冲突 → 中断收尾 + 残留孤儿草稿
            var phAcc = config.DraftPlaceholderAccountId ?? 0;
            if (phAcc == 0)
            {
                warnings.Add($"createDraft 未配占位科目(draftPlaceholderAccountId)，{allUnmatchedRows.Count}行未匹配暂不生成待补录草稿");
            }
            else
            {
                foreach (var (dateKey, dateRows) in GroupRowsByDateField(allUnmatchedRows, config.DateField))
                {
                    DateTime voucherDate = dateKey ?? DateTime.Now;
                    var period = await _dbContext.Set<FinAccountPeriod>()
                        .FirstOrDefaultAsync(p => p.FAccountSetId == accountSetId
                            && p.FStartDate <= voucherDate && p.FEndDate >= voucherDate);
                    if (period == null)
                    {
                        warnings.Add($"待补录草稿: 日期{voucherDate:yyyy-MM-dd}未找到会计期间(账套{accountSetId})");
                        continue;
                    }
                    var scopeId = $"{ctx.BatchId}_unmatched_{(dateKey?.ToString("yyyyMMdd") ?? "nodate")}";
                    var exists = await connection.QueryFirstOrDefaultAsync<long?>(
                        "SELECT TOP 1 FID FROM [FIN凭证] WHERE [F数据作用域ID] = @Key", new { Key = scopeId });
                    if (exists.HasValue) continue;   // 去重：重跑不重复建草稿
                    var draftReq = BuildDraftRequest(config, dateRows, voucherDate, period.FID, ctx.BatchId, scopeId);
                    if (draftReq.Entries[0].DebitAmount == 0m)
                        warnings.Add($"待补录草稿金额为0(批次{ctx.BatchId} 日期{(dateKey?.ToString("yyyyMMdd") ?? "nodate")})，请确认数据源金额列名");
                    var draft = await _voucherService.SaveDraftAsync(draftReq, "数据导入系统", accountSetId);
                    generatedVoucherIds.Add(draft.Id);
                    _logger.LogInformation("AutoVoucher V2: 生成待补录草稿凭证 {VoucherId}, 日期={Date}, 未匹配行数={Count}",
                        draft.Id, voucherDate.ToString("yyyy-MM-dd"), dateRows.Count);
                }
            }
        }

        // 8. 写入 CF凭证生成记录
        var record = new CfVoucherRecord
        {
            FBatchId = ctx.BatchId,
            FTargetTable = stagingTable,
            FTotalRows = filteredRows.Count,
            FMatchedRows = filteredRows.Count - totalUnmatched - skippedRows,
            FUnmatchedRows = totalUnmatched,
            FUnmatchedDetailsJson = warnings.Count > 0 ? JsonSerializer.Serialize(warnings) : null,
            FGeneratedVoucherCount = generatedVoucherIds.Count,
            FVoucherIdsJson = generatedVoucherIds.Count > 0 ? JsonSerializer.Serialize(generatedVoucherIds) : null,
            FStatus = failedGroups == 0 && generatedVoucherIds.Count > 0 ? 1
                : (generatedVoucherIds.Count > 0 ? 2 : 3)
        };
        _dbContext.Set<CfVoucherRecord>().Add(record);
        await _dbContext.SaveChangesAsync();

        // 9. [H7] critical 决策矩阵
        var totalGroups = successGroups + failedGroups;
        bool critical = false;
        if (totalGroups > 0 && (double)failedGroups / totalGroups > 0.5)
            critical = true;
        if (config.UnmatchedAction == "error" && totalUnmatched > 0)
            critical = true;

        var resultMsg = $"V2凭证生成完成: 成功{successGroups}组, 失败{failedGroups}组, 跳过{skippedRows}行, 生成{generatedVoucherIds.Count}张凭证";

        if (critical || (generatedVoucherIds.Count == 0 && successGroups == 0 && failedGroups == 0 && skippedRows == 0))
        {
            var result = HandlerResult.Fail(resultMsg);
            result.Output["Critical"] = critical;
            result.Output["Warnings"] = warnings;
            result.Output["GeneratedVoucherIds"] = generatedVoucherIds;
            return result;
        }
        // 全部跳过（凭证已存在）时，认为是成功（无新数据需写入）
        if (generatedVoucherIds.Count == 0 && !critical)
        {
            var skippedResult = HandlerResult.Ok(resultMsg);
            skippedResult.Output["Critical"] = false;
            skippedResult.Output["Warnings"] = warnings;
            skippedResult.Output["GeneratedVoucherIds"] = generatedVoucherIds;
            return skippedResult;
        }

        var handlerResult = HandlerResult.Ok(resultMsg);
        handlerResult.Output["Critical"] = critical;
        handlerResult.Output["GeneratedVoucherIds"] = generatedVoucherIds;
        handlerResult.Output["SuccessGroups"] = successGroups;
        handlerResult.Output["FailedGroups"] = failedGroups;
        handlerResult.Output["SkippedRows"] = skippedRows;
        handlerResult.Output["Warnings"] = warnings;
        return handlerResult;
    }

    /// <summary>
    /// V2 构建分录行
    /// </summary>
    private void BuildEntryLinesV2(
        Dictionary<int, List<IDictionary<string, object>>> assigned,
        RuleGroupV2 ruleGroup,
        IDictionary<string, object> firstRow,
        AutoVoucherAuxiliaryResolver auxResolver,
        List<V2VoucherEntryDto> entries,
        List<string> warnings,
        Dictionary<long, string?> accountAuxMap)
    {
        foreach (var line in ruleGroup.Lines.Where(l => l.Status == 1).OrderBy(l => l.DisplayOrder))
        {
            if (!assigned.TryGetValue(line.LineNo, out var lineRows) || lineRows.Count == 0)
                continue;

            // 金额计算
            decimal amount = 0;
            if (ruleGroup.AmountAggregation == "SUM")
            {
                amount = lineRows.Sum(r =>
                {
                    var val = r.TryGetValue(line.AmountField, out var v) ? v?.ToString() : null;
                    return decimal.TryParse(val, out var d) ? d : 0;
                });
            }
            else // ROW
            {
                var val = lineRows.First().TryGetValue(line.AmountField, out var v) ? v?.ToString() : null;
                amount = decimal.TryParse(val, out var d) ? d : 0;
            }

            // [H3] 金额为0跳过
            if (amount == 0) continue;

            // 科目解析
            long? accountId = ResolveAccountIdV2(line, firstRow, warnings);

            // 摘要模板渲染
            string summary = RenderSummaryTemplateV2(line.SummaryTemplate, firstRow);

            // 辅助核算
            var auxResults = auxResolver.ResolveAuxiliary(firstRow, line.AuxiliaryConfigs);
            // 方案B 源打标(C)：按科目契约补齐缺失维度——科目声明了辅助核算维度但本行未覆盖的,
            // 用 line.DefaultAuxiliaryConfigs 再解析一轮补上(business_direction=OUT/IN/CMB、project、department 等)。
            // 无 DefaultAuxiliaryConfigs 配置时整段不生效，不改变现有自动凭证行为(additive)。
            if (accountId.HasValue && line.DefaultAuxiliaryConfigs is { Count: > 0 }
                && accountAuxMap.TryGetValue(accountId.Value, out var fAux))
            {
                var present = auxResults.Select(a => a.Type).ToHashSet();
                var missing = AccountAuxContract.GetMissingAuxTypes(fAux, present);
                if (missing.Count > 0)
                {
                    var fillConfigs = line.DefaultAuxiliaryConfigs.Where(c => missing.Contains(c.AuxType)).ToList();
                    if (fillConfigs.Count > 0)
                        auxResults.AddRange(auxResolver.ResolveAuxiliary(firstRow, fillConfigs));
                }
            }
            string? auxiliaryJson = auxResults.Count > 0 ? JsonSerializer.Serialize(auxResults) : null;

            entries.Add(new V2VoucherEntryDto
            {
                Direction = line.Direction,
                AccountId = accountId,
                Amount = amount,
                Summary = summary,
                AuxiliaryJson = auxiliaryJson,
                DisplayOrder = line.DisplayOrder
            });
        }
    }

    /// <summary>
    /// V2 科目解析：固定科目 → 动态匹配 → 兜底科目
    /// </summary>
    internal static long? ResolveAccountIdV2(EntryLineV2 line, IDictionary<string, object> row, List<string> warnings)
    {
        // 1. 固定科目
        if (line.AccountId.HasValue)
            return line.AccountId.Value;

        // 2. 动态科目匹配
        if (!string.IsNullOrEmpty(line.AccountMatchField) && line.AccountMatchRules is { Count: > 0 })
        {
            var matchValue = row.TryGetValue(line.AccountMatchField, out var mv) ? mv?.ToString()?.Trim() ?? "" : "";
            var matched = line.AccountMatchRules.FirstOrDefault(r =>
                string.Equals(r.MatchValue, matchValue, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
                return matched.AccountId;
        }

        // 3. [D6] 兜底科目
        if (line.DefaultAccountId.HasValue)
            return line.DefaultAccountId.Value;

        warnings.Add($"分录行{line.LineNo}科目未命中且无兜底科目");
        return null;
    }

    /// <summary>
    /// V2 摘要模板渲染 [J4] 变量不存在时输出空串
    /// </summary>
    internal static string RenderSummaryTemplateV2(string? template, IDictionary<string, object> row)
    {
        if (string.IsNullOrEmpty(template)) return "";
        return Regex.Replace(template, @"\{(\w+)\}", m =>
        {
            var fieldName = m.Groups[1].Value;
            return row.TryGetValue(fieldName, out var v) ? v?.ToString() ?? "" : "";
        });
    }

    /// <summary>
    /// 按业务日期字段将数据行分组，日期为null的行归入“无日期”组
    /// </summary>
    private static List<(DateTime? DateKey, List<IDictionary<string, object>> Rows)> GroupRowsByDateField(
        List<IDictionary<string, object>> rows, string? dateField)
    {
        if (string.IsNullOrEmpty(dateField))
        {
            // 无日期字段配置，所有行作为一组（日期null）
            return new List<(DateTime?, List<IDictionary<string, object>>)> { (null, rows) };
        }

        var result = new List<(DateTime? DateKey, List<IDictionary<string, object>> Rows)>();
        var dateIndex = new Dictionary<string, int>(); // dateString -> index in result

        foreach (var row in rows)
        {
            DateTime? dateKey = null;
            if (row.TryGetValue(dateField, out var dv) && dv != null)
            {
                if (dv is DateTime dt)
                    dateKey = dt.Date;
                else if (DateTime.TryParse(dv.ToString(), out var parsed))
                    dateKey = parsed.Date;
            }

            var key = dateKey?.ToString("yyyy-MM-dd") ?? "__null__";
            if (!dateIndex.TryGetValue(key, out var idx))
            {
                idx = result.Count;
                dateIndex[key] = idx;
                result.Add((dateKey, new List<IDictionary<string, object>>()));
            }
            result[idx].Rows.Add(row);
        }

        return result;
    }

    /// <summary>
    /// [D10] 构建未匹配行的待补录草稿凭证请求。
    /// 金额逐行累加(收入列+支出列，一行只一列非0)；借贷两行用占位科目同额，
    /// Source="费用支出"、Remark含[待补录]，配合 SaveDraftAsync 写 FStatus=0 草稿(允许 AccountId=0)。
    /// </summary>
    /// <remarks>
    /// 金额列名 <c>F发生额收入</c> / <c>F发生额支出</c> 与申通总部交易明细(StgShentongHqTx / 规则21)数据源硬绑定。
    /// 若 createDraft 推广到其它 STG 数据源，必须将金额列名参数化（如新增 <c>amountColumns</c> 参数），
    /// 否则 TryGetValue 永远不命中、金额累加为 0，草稿凭证静默生成但金额全为 0（漏记风险）。
    /// </remarks>
    internal static CreateVoucherRequest BuildDraftRequest(
        RulesBasedVoucherConfigV2 config, List<IDictionary<string, object>> rows,
        DateTime voucherDate, long periodId, long batchId, string scopeId)
    {
        decimal total = 0m;
        foreach (var r in rows)
            foreach (var col in new[] { "F发生额收入", "F发生额支出" })
                if (r.TryGetValue(col, out var v) && decimal.TryParse(v?.ToString(), out var d)) total += Math.Abs(d);
        long acc = config.DraftPlaceholderAccountId ?? 0;
        var entries = new List<CreateVoucherEntryRequest> {
            new() { LineNo = 1, Summary = "[待补录]未匹配行", AccountId = acc, DebitAmount = total, CreditAmount = 0 },
            new() { LineNo = 2, Summary = "[待补录]未匹配行", AccountId = acc, DebitAmount = 0, CreditAmount = total },
        };
        return new CreateVoucherRequest {
            VoucherWord = config.VoucherWord, Date = voucherDate, PeriodId = periodId,
            Source = "费用支出", Remark = $"[待补录] 数据导入未匹配(V2) - 批次{batchId}",
            DataScopeId = scopeId, Entries = entries
        };
    }

    /// <summary>
    /// [H2] 计算凭证业务键（用于去重）
    /// </summary>
    internal static string? ComputeBusinessKeyV2(
        List<IDictionary<string, object>> rows, List<string>? keyFields, long batchId)
    {
        if (keyFields == null || keyFields.Count == 0)
            return null;

        // 取首行的 keyFields 值拼接为业务键
        var firstRow = rows.FirstOrDefault();
        if (firstRow == null) return null;

        var keyParts = new List<string> { batchId.ToString() };
        foreach (var field in keyFields)
        {
            var val = firstRow.TryGetValue(field, out var v) ? v?.ToString() ?? "" : "";
            keyParts.Add($"{field}={val}");
        }
        return string.Join("|", keyParts);
    }

    /// <summary>
    /// V2 内部分录 DTO
    /// </summary>
    private class V2VoucherEntryDto
    {
        public string Direction { get; set; } = "借";
        public long? AccountId { get; set; }
        public decimal Amount { get; set; }
        public string? Summary { get; set; }
        public string? AuxiliaryJson { get; set; }
        public int DisplayOrder { get; set; }
    }

    #endregion

    #region V2 DryRun

    /// <summary>
    /// V2 DryRun 预演 — 供 AgentRuleController 直接调用，纯计算不持久化。
    /// 返回与前端 DryRunPanel.vue 兼容的 DryRunResult。
    /// </summary>
    public async Task<DryRunResult> HandleV2DryRunAsync(
        RulesBasedVoucherConfigV2 config, long batchId, long orgId, string? groupField = null)
    {
        // 1. 校验
        var stagingTable = config.StagingTable;
        if (string.IsNullOrEmpty(stagingTable) || !ValidTableNameRegex().IsMatch(stagingTable))
            throw new InvalidOperationException($"非法或缺失的暂存表名: {stagingTable}");
        if (config.RuleGroups.Count == 0)
            throw new InvalidOperationException("RuleGroups 为空");

        // 2. 加载 STG 数据
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        var rows = (await connection.QueryAsync(
                $"SELECT * FROM [{stagingTable}] WHERE [F批次ID] = @batchId",
                new { batchId }))
            .Select(r => (IDictionary<string, object>)r)
            .ToList();

        if (rows.Count == 0)
            throw new InvalidOperationException("暂存表无数据");

        // 3. 预筛选 FilterConditions
        var engine = new AutoVoucherMatchingEngineV2();
        var filteredRows = engine.ApplyFilterConditions(rows, config.FilterConditions);
        if (filteredRows.Count == 0)
            throw new InvalidOperationException("预筛选后无可处理数据");

        // 4. 初始化匹配引擎
        engine.Initialize(config);

        // 5. 逐行匹配，收集统计
        int totalRows = filteredRows.Count;
        int matchedRows = 0;
        var matchedRowIds = new HashSet<long>();
        var groupStats = config.RuleGroups.ToDictionary(g => g.Id, _ => 0);

        foreach (var row in filteredRows)
        {
            var candidates = engine.MatchRowToRuleGroup(row);
            var (groupId, routedButNoOutput) = engine.ResolveFinalGroup(row, candidates);

            if (groupId == null || routedButNoOutput) continue;

            matchedRows++;
            if (row.TryGetValue("FID", out var fidVal) && fidVal != null)
                matchedRowIds.Add(Convert.ToInt64(fidVal));
            if (groupStats.ContainsKey(groupId))
                groupStats[groupId]++;
        }

        // 6. 收集未匹配行
        var unmatchedRowData = filteredRows.Where(r =>
        {
            if (r.TryGetValue("FID", out var f) && f != null)
                return !matchedRowIds.Contains(Convert.ToInt64(f));
            return true;
        }).ToList();

        const int maxUnmatchedReturn = 500;
        var hasMore = unmatchedRowData.Count > maxUnmatchedReturn;
        var unmatchedToReturn = unmatchedRowData.Take(maxUnmatchedReturn)
            .Select(r => r.ToDictionary(kv => kv.Key, kv => kv.Value))
            .ToList();

        // 7. 构建 GroupDetails（按规则组序号 + 分录行号统计）
        var groupDetails = new List<DryRunGroupDetail>();
        for (int gi = 0; gi < config.RuleGroups.Count; gi++)
        {
            var g = config.RuleGroups[gi];
            int gHits = groupStats.GetValueOrDefault(g.Id, 0);
            foreach (var line in g.Lines.Where(l => l.Status == 1))
            {
                groupDetails.Add(new DryRunGroupDetail
                {
                    GroupIndex = gi,
                    LineNo = line.LineNo,
                    Matched = gHits
                });
            }
        }

        // 8. 估计凭证数
        int estimatedVouchers = 0;
        if (string.IsNullOrEmpty(config.GroupBy))
        {
            estimatedVouchers = matchedRows > 0 ? 1 : 0;
        }
        else
        {
            estimatedVouchers = filteredRows
                .Where(r => r.TryGetValue("FID", out var f) && f != null && matchedRowIds.Contains(Convert.ToInt64(f)))
                .GroupBy(r => r.TryGetValue(config.GroupBy, out var gv) ? gv?.ToString() ?? "" : "")
                .Count();
        }

        // 9. 按字段汇总未匹配行
        var effectiveGroupField = !string.IsNullOrWhiteSpace(groupField) ? groupField : config.GroupBy;
        var groupedSummary = new List<DryRunGroupedSummary>();
        if (!string.IsNullOrWhiteSpace(effectiveGroupField) && unmatchedRowData.Count > 0)
        {
            var amountField = unmatchedRowData[0].Keys.FirstOrDefault(k =>
                k.Contains("金额") || k.Contains("Amount", StringComparison.OrdinalIgnoreCase));

            groupedSummary = unmatchedRowData
                .GroupBy(r => r.ContainsKey(effectiveGroupField)
                    ? r[effectiveGroupField]?.ToString() ?? "(空)" : "(无此字段)")
                .Select(g => new DryRunGroupedSummary
                {
                    FieldValue = g.Key,
                    Count = g.Count(),
                    TotalAmount = amountField != null
                        ? g.Sum(r => r.TryGetValue(amountField, out var val) && val != null
                            && decimal.TryParse(val.ToString(), out var d) ? d : 0m)
                        : null
                })
                .OrderByDescending(s => s.Count)
                .ToList();
        }

        return new DryRunResult
        {
            TotalRows = totalRows,
            MatchedRows = matchedRows,
            UnmatchedRows = unmatchedRowData.Count,
            EstimatedVouchers = estimatedVouchers,
            GroupDetails = groupDetails,
            UnmatchedDetails = unmatchedToReturn,
            HasMoreUnmatched = hasMore,
            GroupedSummary = groupedSummary
        };
    }

    #endregion

    #region 未匹配行诊断

    /// <summary>
    /// 构建未匹配行的诊断明细文本
    /// <para>从 row 中提取匹配层字段值，帮助定位未匹配原因</para>
    /// </summary>
    private static string BuildUnmatchedRowDetail(IDictionary<string, object> row, MatchingLayerConfig layers)
    {
        var parts = new List<string>();

        // 提取三层匹配字段值
        if (!string.IsNullOrEmpty(layers.ExactMatchField) &&
            row.TryGetValue(layers.ExactMatchField, out var ev) && ev != null)
            parts.Add($"{layers.ExactMatchField}={ev}");
        if (!string.IsNullOrEmpty(layers.CategoryField) &&
            row.TryGetValue(layers.CategoryField, out var cv) && cv != null)
            parts.Add($"{layers.CategoryField}={cv}");
        if (!string.IsNullOrEmpty(layers.SummaryField) &&
            row.TryGetValue(layers.SummaryField, out var sv) && sv != null)
            parts.Add($"{layers.SummaryField}={sv}");

        // 补充常见业务字段（如存在）
        foreach (var key in new[] { "F网点编号", "F网点名称", "F费用类别", "F业务摘要" })
        {
            if (row.TryGetValue(key, out var v) && v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                parts.Add($"{key}={v}");
        }

        // 去重
        var distinct = parts.Distinct().ToList();
        return distinct.Count > 0 ? "  示例行: " + string.Join(", ", distinct) : "  示例行: (无可识别字段)";
    }

    #endregion

}

/// <summary>AutoPlugin 透传的运行时上下文</summary>
public class AutoVoucherRuntimeContext
{
    public long BatchId { get; set; }
    public long OrgId { get; set; }
}
