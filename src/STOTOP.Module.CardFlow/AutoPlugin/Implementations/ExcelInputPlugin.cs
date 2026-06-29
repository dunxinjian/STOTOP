using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Import;
using STOTOP.Module.CardFlow.Services.Import.TransformEngine;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>
/// Excel输入插件 — 从Excel/CSV文件读取数据写入暂存表。
/// 适配自 DataCenter ExcelInputAgent，配置全部从 CfPluginRule 加载。
/// </summary>
public class ExcelInputPlugin : InputPluginBase
{
    public override string PluginName => "ExcelInput";
    public override string DisplayName => "Excel导入";
    private const int RowProgressInterval = 1000;

    private readonly ExcelParserService _excelParser;
    private readonly IBulkInsertService _bulkInsertService;
    private readonly ITransformEngine _transformEngine;
    private readonly IPluginProgressReporter _progressReporter;
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<ExcelInputPlugin> _logger;
    private readonly IConfiguration _configuration;

    public ExcelInputPlugin(
        ExcelParserService excelParser,
        IBulkInsertService bulkInsertService,
        ITransformEngine transformEngine,
        IPluginProgressReporter progressReporter,
        STOTOPDbContext dbContext,
        ILogger<ExcelInputPlugin> logger,
        IConfiguration configuration)
    {
        _excelParser = excelParser;
        _bulkInsertService = bulkInsertService;
        _transformEngine = transformEngine;
        _progressReporter = progressReporter;
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
    }

    // 配置字段（从 CfPluginRule 解析）───
    private List<ColumnMappingItem> _columnMappings = new();
    private HashSet<string> _decimalFields = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _keyFields = new();
    private string? _serialNumberRule;
    private TotalRowDetectionConfig? _totalRowConfig;
    private List<TransformRule> _transformRules = new();
    private string _targetTable = string.Empty;
    private int _headerRow = 1;
    private int _dataStartRow = 0;
    // 可选：指定要解析的 Excel sheet 名（多 sheet 源用，如物流信息指数）。为空则读首表
    private string? _sheetName;

    // 批次拆分配置
    private bool _batchSplitEnabled;
    private string _splitField = string.Empty;
    private string _targetTableTemplate = string.Empty;

    // 跨批次去重配置
    private List<string> _crossBatchDedupFields = new();
    private bool _crossBatchDedupEnabled = false;

    // 输出模式配置
    /// <summary>输出模式：stg=写入暂存表（默认）/ batchRow=写入CfBatchRow / both=两者都写</summary>
    private string _outputMode = "stg";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    // ═══════════════════════════════════════════════════════════════
    //  ExecuteAsync — 主入口
    // ═══════════════════════════════════════════════════════════════
    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        // batchRow 模式少一步暂存表检查；both 模式多一步 batchRow 写入
        var totalSteps = _outputMode switch
        {
            "batchRow" => 5,
            "both" => 7,
            _ => 6
        };
        try
        {
            // 1. 解析配置
            await _progressReporter.ReportProgressAsync(context.BatchId, 0, totalSteps, "解析配置");
            await ParseConfigAsync(context);
            await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "解析配置");

            // 1.5 尽早持久化 FActualTargetTable（仅 stg/both 模式）
            if (_outputMode != "batchRow")
            {
                try
                {
                    var batchForTarget = await _dbContext.Set<CfBatch>()
                        .FirstOrDefaultAsync(b => b.FID == context.BatchId);
                    if (batchForTarget != null && string.IsNullOrEmpty(batchForTarget.FActualTargetTable))
                    {
                        batchForTarget.FActualTargetTable = _targetTable;
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation(
                            "ExcelInputPlugin: 已提前持久化 FActualTargetTable={Table}, 批次={BatchId}",
                            _targetTable, context.BatchId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "ExcelInputPlugin: 提前持久化 FActualTargetTable 失败（不影响主流程），批次={BatchId}",
                        context.BatchId);
                }
            }

            // 2. 暂存表检查 — 仅 stg/both 模式需要
            if (_outputMode != "batchRow")
            {
                await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "暂存表检查");
                var checkResult = await CheckStagingTableAsync();
                if (!checkResult.Success)
                    return checkResult;
                await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "暂存表检查");
            }

            // 3. 读取Excel数据
            await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "读取Excel数据");
            var allRows = await ReadExcelDataAsync(context);
            if (allRows.Count == 0)
                return PluginResult.Fail("Excel文件无有效数据行", isCritical: true);
            await _progressReporter.ReportProgressAsync(context.BatchId, 3, totalSteps, "读取Excel数据");

            // 4. 数据转换
            await _progressReporter.ReportProgressAsync(context.BatchId, 3, totalSteps, "数据转换");

            // 5. 批次拆分
            await _progressReporter.ReportProgressAsync(context.BatchId, 4, totalSteps, "批次拆分");
            var splits = SplitBatches(allRows);
            await _progressReporter.ReportProgressAsync(context.BatchId, 5, totalSteps, "批次拆分");

            // 6. 逐批次导入（仅 stg/both 模式）
            int totalSuccess = 0, totalFail = 0, totalSkipped = 0;
            var createdBatches = new List<ChildBatchInfo>();

            if (_outputMode != "batchRow")
            {
                await _progressReporter.ReportProgressAsync(context.BatchId, 5, totalSteps, "数据导入");

                foreach (var split in splits)
                {
                    var importResult = await ImportBatchAsync(context, split);
                    if (importResult.CreatedBatches != null)
                        createdBatches.AddRange(importResult.CreatedBatches);

                    totalSuccess += importResult.ProcessedRows;
                    totalFail += importResult.FailedRows;
                    totalSkipped += importResult.SkippedRows;

                    if (!importResult.Success && importResult.IsCritical)
                        return importResult;
                }
                await _progressReporter.ReportProgressAsync(context.BatchId, 6, totalSteps, "数据导入");
            }

            // 6.5 batchRow 模式：将数据写入 CfBatchRow
            if (_outputMode == "batchRow" || _outputMode == "both")
            {
                await WriteToBatchRowsAsync(context, allRows);
                totalSuccess = allRows.Count;
            }

            _logger.LogInformation(
                "ExcelInputPlugin 完成: 批次={BatchId}, 总行={Total}, 成功={Success}, 失败={Fail}, 跳过={Skipped}, 子批次={SubCount}, 输出模式={OutputMode}",
                context.BatchId, allRows.Count, totalSuccess, totalFail, totalSkipped, createdBatches.Count, _outputMode);

            if (_outputMode != "batchRow" && totalSuccess == 0 && totalFail == 0 && totalSkipped > 0)
            {
                return PluginResult.Fail(
                    $"Excel导入未产生新数据：总{allRows.Count}行，跳过{totalSkipped}行。可能是跨批次去重命中已有数据，已停止后续价格/成本计算。");
            }

            var ok = PluginResult.Ok(
                $"Excel导入完成(outputMode={_outputMode}): 总{allRows.Count}行, 成功{totalSuccess}行, 跳过{totalSkipped}行",
                totalSuccess);
            ok.FailedRows = totalFail;
            ok.SkippedRows = totalSkipped;
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExcelInputPlugin 执行异常: 批次={BatchId}, 输出模式={OutputMode}", context.BatchId, _outputMode);
            return PluginResult.Fail($"ExcelInputPlugin 异常: {ex.Message}", isCritical: true);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  WriteToBatchRowsAsync — batchRow 模式写入
    // ═══════════════════════════════════════════════════════════════
    private async Task WriteToBatchRowsAsync(PluginContext context, List<Dictionary<string, string>> allRows)
    {
        var batchRows = new List<CfBatchRow>();
        for (int i = 0; i < allRows.Count; i++)
        {
            var row = allRows[i];
            // 根据 columnMapping 映射字段名（Excel列名 → Schema字段名）
            var mappedRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in _columnMappings)
            {
                if (row.TryGetValue(mapping.ExcelColumn, out var value) && !string.IsNullOrEmpty(value))
                    mappedRow[mapping.DbColumn] = value;
                else if (mapping.Aliases != null)
                {
                    // 尝试别名匹配
                    var aliasMatch = mapping.Aliases.FirstOrDefault(a => row.TryGetValue(a, out var av) && !string.IsNullOrEmpty(av));
                    if (aliasMatch != null)
                        mappedRow[mapping.DbColumn] = row[aliasMatch];
                }
            }

            batchRows.Add(new CfBatchRow
            {
                FBatchId = context.BatchId,
                FRowNo = i + 1,
                FDataJson = JsonSerializer.Serialize(mappedRow, JsonOpts),
                FStatus = 0,  // 待质检
                FCreatedTime = DateTime.Now
            });

            var current = i + 1;
            if (ShouldReportRowProgress(current, allRows.Count))
                await _progressReporter.ReportProgressAsync(context.BatchId, current, allRows.Count, "写入批次行");
        }

        _dbContext.Set<CfBatchRow>().AddRange(batchRows);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "ExcelInputPlugin: batchRow 模式写入 {Count} 行到 CfBatchRow, 批次={BatchId}",
            batchRows.Count, context.BatchId);
    }

    // ═══════════════════════════════════════════════════════════════
    //  RollbackAsync
    // ═══════════════════════════════════════════════════════════════
    public override async Task RollbackAsync(PluginContext context)
    {
        // ── 0. batchRow/both 模式：清理 CfBatchRow 数据 ──
        if (_outputMode == "batchRow" || _outputMode == "both")
        {
            var batchRows = await _dbContext.Set<CfBatchRow>()
                .Where(r => r.FBatchId == context.BatchId)
                .ToListAsync();
            if (batchRows.Count > 0)
            {
                _dbContext.Set<CfBatchRow>().RemoveRange(batchRows);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation(
                    "RollbackAsync: 已清理 {Count} 条 CfBatchRow 数据, 批次={BatchId}",
                    batchRows.Count, context.BatchId);
            }
        }

        // ── STG 数据清理（stg/both 模式） ──
        var tableName = _targetTable;
        if (string.IsNullOrEmpty(tableName))
        {
            var batchRecord = await _dbContext.Set<CfBatch>()
                .AsNoTracking()
                .Where(b => b.FID == context.BatchId)
                .Select(b => b.FActualTargetTable)
                .FirstOrDefaultAsync();
            tableName = batchRecord;
        }

        if (string.IsNullOrEmpty(tableName))
        {
            _logger.LogWarning("RollbackAsync: 无法获取目标表名，批次={BatchId}", context.BatchId);
            return;
        }

        // ── 1. 清理父批次自身的 STG 数据 ──
        await DeleteStagingDataByBatchId(context.BatchId, tableName);

        // ── 2. 清理所有子批次的 STG 数据 ──
        var children = await _dbContext.Set<CfBatch>()
            .Where(b => b.FOrchestrationInstanceId == context.BatchId)
            .Select(b => new { b.FID, b.FActualTargetTable })
            .ToListAsync();
        foreach (var child in children)
        {
            var childTable = child.FActualTargetTable ?? tableName;
            if (!string.IsNullOrEmpty(childTable))
                await DeleteStagingDataByBatchId(child.FID, childTable);
        }

        _logger.LogInformation(
            "RollbackAsync: 已清理父批次 {BatchId} 及 {ChildCount} 个子批次的 STG 数据",
            context.BatchId, children.Count);
    }

    private async Task DeleteStagingDataByBatchId(long batchId, string tableName)
    {
        StagingTableNameValidator.EnsureSafe(tableName);
        var connStr = DbConnectionsHelper.GetSystemConnectionString(_configuration.GetValue<string>("Security:EncryptionKey"));
        if (string.IsNullOrEmpty(connStr))
            throw new InvalidOperationException($"DeleteStagingDataByBatchId 失败：无法获取系统数据库连接字符串，批次={batchId}，表={tableName}");

        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM [{tableName}] WHERE [F批次ID] = @batchId";
        cmd.Parameters.AddWithValue("@batchId", batchId);
        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows > 0)
            _logger.LogInformation("DeleteStagingData: 批次={BatchId}, 表={Table}, 清理 {Rows} 行",
                batchId, tableName, rows);
    }

    // ═══════════════════════════════════════════════════════════════
    //  SplitBatches
    // ═══════════════════════════════════════════════════════════════
    private List<BatchSplitResult> SplitBatches(List<Dictionary<string, string>> allRows)
    {
        var results = new List<BatchSplitResult>();

        if (_batchSplitEnabled && !string.IsNullOrEmpty(_splitField))
        {
            var groups = allRows.GroupBy(r => r.TryGetValue(_splitField, out var v) ? v ?? "" : "");
            foreach (var group in groups)
            {
                var splitKey = group.Key;
                var targetTbl = string.IsNullOrEmpty(_targetTableTemplate)
                    ? _targetTable
                    : _targetTableTemplate
                                            .Replace("{splitValue}", splitKey)
                                            .Replace("{value}", splitKey);
                results.Add(new BatchSplitResult
                {
                    SplitKey = splitKey,
                    TargetTable = targetTbl,
                    Rows = group.Select(r => r.ToDictionary(kv => kv.Key, kv => (object)kv.Value)).ToList()
                });
            }
            return results;
        }

        results.Add(new BatchSplitResult
        {
            SplitKey = string.Empty,
            TargetTable = _targetTable,
            Rows = allRows.Select(r => r.ToDictionary(kv => kv.Key, kv => (object)kv.Value)).ToList()
        });
        return results;
    }

    // ═══════════════════════════════════════════════════════════════
    //  ImportBatchAsync
    // ═══════════════════════════════════════════════════════════════
    private async Task<PluginResult> ImportBatchAsync(PluginContext context, BatchSplitResult split)
    {
        var targetTable = split.TargetTable;

        if (string.IsNullOrEmpty(targetTable))
            return PluginResult.Fail("目标暂存表名为空");

        StagingTableNameValidator.EnsureSafe(targetTable);

        var connStr = DbConnectionsHelper.GetSystemConnectionString(_configuration.GetValue<string>("Security:EncryptionKey"));
        if (string.IsNullOrEmpty(connStr))
            return PluginResult.Fail("未找到系统数据库连接字符串");

        // 获取批次的组织和账套信息
        var batchInfo = await _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FID == context.BatchId)
            .Select(b => new { b.FOrgId, b.FAccountSetId })
            .FirstOrDefaultAsync();
        var orgId = batchInfo?.FOrgId ?? 0;
        var accountSetId = batchInfo?.FAccountSetId;

        // 如果有多个split，需要创建子批次记录
        long batchId = context.BatchId;
        ChildBatchInfo? childInfo = null;

        if (!string.IsNullOrEmpty(split.SplitKey))
        {
            var parentBatch = await _dbContext.Set<CfBatch>()
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.FID == context.BatchId);

            if (parentBatch != null)
            {
                var expectedBatchNo = $"{parentBatch.FBatchNo}-{split.SplitKey}";

                var existingChild = await _dbContext.Set<CfBatch>()
                    .FirstOrDefaultAsync(b => b.FOrchestrationInstanceId == context.BatchId
                        && b.FBatchNo == expectedBatchNo);

                if (existingChild != null)
                {
                    batchId = existingChild.FID;
                    await DeleteStagingDataByBatchId(batchId, targetTable);

                    existingChild.FStatus = CfBatchStatus.Parsing; // StagingWrite 阶段对应 Parsing
                    existingChild.FTotalRows = 0;
                    existingChild.FSuccessRows = 0;
                    existingChild.FFailedRows = 0;
                    existingChild.FActualTargetTable = targetTable;
                    await _dbContext.SaveChangesAsync();

                    childInfo = new ChildBatchInfo { BatchId = existingChild.FID, BatchName = existingChild.FBatchNo };
                }
                else
                {
                    var subBatch = new CfBatch
                    {
                        FBatchNo = expectedBatchNo,
                        FFlowDefinitionId = parentBatch.FFlowDefinitionId,
                        FOrchestrationInstanceId = context.BatchId, // 用编排实例ID表示父子关系
                        FActualTargetTable = targetTable,
                        FFileName = parentBatch.FFileName,
                        FFilePath = parentBatch.FFilePath,
                        FFileHash = parentBatch.FFileHash,
                        FFileSize = parentBatch.FFileSize,
                        FTotalRows = 0,
                        FSuccessRows = 0,
                        FFailedRows = 0,
                        FStatus = CfBatchStatus.Parsing, // StagingWrite 阶段对应 Parsing
                        FUploadMethod = parentBatch.FUploadMethod,
                        FOrgId = parentBatch.FOrgId,
                        FAccountSetId = parentBatch.FAccountSetId,
                        FTriggeredById = parentBatch.FTriggeredById,
                        FTriggerType = parentBatch.FTriggerType,
                        FCreatedTime = DateTime.Now,
                    };
                    _dbContext.Set<CfBatch>().Add(subBatch);
                    await _dbContext.SaveChangesAsync();
                    batchId = subBatch.FID;

                    childInfo = new ChildBatchInfo { BatchId = subBatch.FID, BatchName = subBatch.FBatchNo };
                }
            }
        }

        // 构建列映射集合
        var mappedExcelColumns = BuildMappedExcelColumnsSet();
        var dt = CreateDataTable();

        int successRows = 0, failRows = 0, skippedRows = 0;
        int rowIndex = 0;
        int totalRows = split.Rows.Count;
        var seenBusinessKeys = new HashSet<string>();

        // 跨批次去重
        var crossBatchDupCount = 0;
        var crossBatchDedupActive = _crossBatchDedupEnabled && _crossBatchDedupFields.Count > 0;

        if (crossBatchDedupActive)
        {
            foreach (var field in _crossBatchDedupFields)
            {
                if (!dt.Columns.Contains(field))
                {
                    _logger.LogWarning("跨批次去重字段 [{Field}] 在DataTable中不存在，去重将禁用", field);
                    crossBatchDedupActive = false;
                    break;
                }
            }
        }

        var pendingRows = new List<(DataRow Row, int RowIndex)>();

        foreach (var rawRow in split.Rows)
        {
            rowIndex++;
            var row = rawRow.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty);

            if (_totalRowConfig?.Enabled == true && IsTotalRow(row))
            {
                skippedRows++;
            }
            else
            {
                try
                {
                    var dr = dt.NewRow();
                    dr["F批次ID"] = batchId;
                    dr["F原始行号"] = rowIndex;
                    dr["FOrgId"] = orgId;
                    dr["F账套ID"] = accountSetId.HasValue ? accountSetId.Value : (object)DBNull.Value;

                    var mappedData = new Dictionary<string, string>();
                    foreach (var m in _columnMappings)
                    {
                        var value = GetValueByMapping(row, m);
                        mappedData[m.DbColumn] = value;

                        if (_decimalFields.Contains(m.DbColumn) && !string.IsNullOrEmpty(value))
                        {
                            if (decimal.TryParse(value, out var dv))
                                dr[m.DbColumn] = dv;
                            else
                                dr[m.DbColumn] = DBNull.Value;
                        }
                        else
                        {
                            dr[m.DbColumn] = string.IsNullOrEmpty(value) ? DBNull.Value : value;
                        }
                    }

                    if (_transformRules.Count > 0)
                    {
                        var transformed = _transformEngine.Execute(mappedData, _transformRules);
                        foreach (var kvp in transformed)
                        {
                            if (dt.Columns.Contains(kvp.Key))
                                dr[kvp.Key] = kvp.Value?.ToString() ?? (object)DBNull.Value;
                        }
                    }

                    if (!string.IsNullOrEmpty(_serialNumberRule))
                        dr["F流水号"] = BuildSerialNumber(row);

                    dr["F其他列数据"] = BuildUnmappedJson(row, mappedExcelColumns);

                    var businessKey = ComputeBusinessKey(row);
                    if (!seenBusinessKeys.Add(businessKey))
                    {
                        _logger.LogWarning("行 {Row} 业务主键重复，已跳过", rowIndex);
                        failRows++;
                    }
                    else
                    {
                        dr["F业务主键"] = businessKey;
                        pendingRows.Add((dr, rowIndex));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "行 {Row} 处理失败: {Msg}", rowIndex, ex.Message);
                    failRows++;
                }
            }

            if (ShouldReportRowProgress(rowIndex, totalRows))
                await _progressReporter.ReportProgressAsync(context.BatchId, rowIndex, totalRows, "解析导入数据");
        }

        // 跨批次去重阶段二
        var crossBatchExistingSet = new HashSet<string>();
        if (crossBatchDedupActive && pendingRows.Count > 0)
        {
            var candidateValues = new List<string[]>();
            foreach (var (dr, _) in pendingRows)
            {
                var values = CrossBatchDedupKeyBuilder.ExtractValues(dr, _crossBatchDedupFields);
                if (values.All(v => !string.IsNullOrWhiteSpace(v)))
                    candidateValues.Add(values);
            }

            if (candidateValues.Count > 0)
            {
                crossBatchExistingSet = await QueryExistingByFieldsAsync(
                    connStr, targetTable, _crossBatchDedupFields,
                    candidateValues, batchId, orgId);
            }
        }

        foreach (var (dr, pendingRowIndex) in pendingRows)
        {
            if (crossBatchDedupActive && crossBatchExistingSet.Count > 0)
            {
                var dedupValues = CrossBatchDedupKeyBuilder.ExtractValues(dr, _crossBatchDedupFields);

                // 全部去重字段非空才参与比对（与候选键构造口径一致），按完整复合键判重
                if (dedupValues.All(v => !string.IsNullOrWhiteSpace(v))
                    && crossBatchExistingSet.Contains(CrossBatchDedupKeyBuilder.BuildKey(dedupValues)))
                {
                    crossBatchDupCount++;
                    continue;
                }
            }

            dt.Rows.Add(dr);
            successRows++;
        }

        // 写入逻辑
        if (dt.Rows.Count > 0)
        {
            try
            {
                await BulkCopyAsync(connStr, targetTable, dt);
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                _logger.LogWarning("批量写入触发唯一约束，执行重查-剔除-重试流程");

                // 若未配置跨批次去重字段，则 fallback 使用 keyFields 对应的 db 列名作为去重依据
                var dedupFields = _crossBatchDedupFields.Count > 0
                    ? _crossBatchDedupFields
                    : _keyFields
                        .Select(excelCol => _columnMappings.FirstOrDefault(m => m.ExcelColumn == excelCol)?.DbColumn)
                        .Where(dbCol => !string.IsNullOrEmpty(dbCol) && dt.Columns.Contains(dbCol!))
                        .Select(dbCol => dbCol!)
                        .ToList();

                if (dedupFields.Count == 0)
                {
                    _logger.LogError("唯一约束冲突但无法确定去重字段，放弃本批 {Count} 行", dt.Rows.Count);
                    crossBatchDupCount += dt.Rows.Count;
                    successRows -= dt.Rows.Count;
                }
                else
                {

                var currentKeys = new List<string[]>();
                foreach (DataRow row in dt.Rows)
                {
                    var vals = CrossBatchDedupKeyBuilder.ExtractValues(row, dedupFields);
                    if (vals.All(v => !string.IsNullOrWhiteSpace(v)))
                        currentKeys.Add(vals);
                }

                var newlyExisting = await QueryExistingByFieldsAsync(
                    connStr, targetTable, dedupFields, currentKeys, batchId, orgId);

                var rowsToRemove = new List<DataRow>();
                foreach (DataRow row in dt.Rows)
                {
                    var vals = CrossBatchDedupKeyBuilder.ExtractValues(row, dedupFields);
                    if (vals.All(v => !string.IsNullOrWhiteSpace(v))
                        && newlyExisting.Contains(CrossBatchDedupKeyBuilder.BuildKey(vals)))
                        rowsToRemove.Add(row);
                }

                foreach (var row in rowsToRemove)
                    dt.Rows.Remove(row);
                crossBatchDupCount += rowsToRemove.Count;
                successRows -= rowsToRemove.Count;

                if (dt.Rows.Count > 0)
                {
                    try
                    {
                        await BulkCopyAsync(connStr, targetTable, dt);
                    }
                    catch (SqlException)
                    {
                        _logger.LogError("重试批量写入仍失败，放弃本批 {Count} 行", dt.Rows.Count);
                        crossBatchDupCount += dt.Rows.Count;
                        successRows -= dt.Rows.Count;
                    }
                }
                } // end else dedupFields.Count > 0
            }
        }

        skippedRows += crossBatchDupCount;

        await UpdateBatchStatsAsync(batchId, targetTable, successRows, failRows, skippedRows);

        var result = PluginResult.Ok();
        result.CreatedBatches = childInfo != null ? new List<ChildBatchInfo> { childInfo } : null;
        result.ProcessedRows = successRows;
        result.FailedRows = failRows;
        result.SkippedRows = skippedRows;
        return result;
    }

    private static bool ShouldReportRowProgress(int current, int total)
    {
        if (total <= 0) return false;
        return current == total || current % RowProgressInterval == 0;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private — 配置解析
    // ═══════════════════════════════════════════════════════════════
    private async Task ParseConfigAsync(PluginContext context)
    {
        if (!context.PluginRuleId.HasValue)
            throw new InvalidOperationException("PluginRuleId 为空，无法执行 ExcelInputPlugin");

        var rule = await _dbContext.Set<CfPluginRule>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FID == context.PluginRuleId.Value);

        if (rule?.F规则配置JSON == null)
            throw new InvalidOperationException("CfPluginRule 配置JSON为空，无法执行 ExcelInputPlugin");

        using var doc = JsonDocument.Parse(rule.F规则配置JSON);
        var root = doc.RootElement;

        _targetTable = GetStringProp(root, "targetTable") ?? string.Empty;
        _headerRow = root.TryGetProperty("headerRow", out var hr) && hr.TryGetInt32(out var hrv) ? hrv : 1;
        _dataStartRow = root.TryGetProperty("dataStartRow", out var dsr) && dsr.TryGetInt32(out var dsrv) ? dsrv : _headerRow + 1;
        _sheetName = GetStringProp(root, "sheetName");

        if (root.TryGetProperty("columnMapping", out var cmProp))
            _columnMappings = JsonSerializer.Deserialize<List<ColumnMappingItem>>(cmProp.GetRawText(), JsonOpts) ?? new();

        if (root.TryGetProperty("decimalFields", out var dfProp))
        {
            var list = JsonSerializer.Deserialize<List<string>>(dfProp.GetRawText(), JsonOpts);
            _decimalFields = list != null ? new HashSet<string>(list, StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase);
        }

        if (root.TryGetProperty("keyFields", out var kfProp))
            _keyFields = JsonSerializer.Deserialize<List<string>>(kfProp.GetRawText(), JsonOpts) ?? new();

        _serialNumberRule = GetStringProp(root, "serialNumberRule");

        if (root.TryGetProperty("totalRowDetection", out var trProp) && trProp.ValueKind == JsonValueKind.Object)
            _totalRowConfig = JsonSerializer.Deserialize<TotalRowDetectionConfig>(trProp.GetRawText(), JsonOpts);

        if (root.TryGetProperty("transformRules", out var trRules))
            _transformRules = JsonSerializer.Deserialize<List<TransformRule>>(trRules.GetRawText(), JsonOpts) ?? new();

        if (root.TryGetProperty("crossBatchDedupEnabled", out var cbdEnabled))
            _crossBatchDedupEnabled = cbdEnabled.GetBoolean();
        if (root.TryGetProperty("crossBatchDedupFields", out var cbdFields))
            _crossBatchDedupFields = JsonSerializer.Deserialize<List<string>>(cbdFields.GetRawText()) ?? new();

        if (root.TryGetProperty("batchSplit", out var bsProp))
        {
            if (bsProp.ValueKind == JsonValueKind.Object)
            {
                if (bsProp.TryGetProperty("enabled", out var bsEn))
                    _batchSplitEnabled = bsEn.GetBoolean();
                _splitField = GetStringProp(bsProp, "splitField") ?? string.Empty;
                _targetTableTemplate = GetStringProp(bsProp, "targetTableTemplate") ?? string.Empty;
            }
            else if (bsProp.ValueKind == JsonValueKind.True || bsProp.ValueKind == JsonValueKind.False)
            {
                _batchSplitEnabled = bsProp.GetBoolean();
            }
        }

        // outputMode 解析（缺失默认 "stg"，向后兼容）
        if (root.TryGetProperty("outputMode", out var omProp) && omProp.ValueKind == JsonValueKind.String)
            _outputMode = omProp.GetString() ?? "stg";

        if (_outputMode != "batchRow" && string.IsNullOrEmpty(_targetTable))
            throw new InvalidOperationException("目标暂存表未配置：请配置 targetTable");

        _logger.LogInformation(
            "ExcelInputPlugin 配置: 目标表={Table}, 列映射={MapCount}, 主键={KeyCount}, 拆分={Split}, 输出模式={OutputMode}",
            _targetTable, _columnMappings.Count, _keyFields.Count, _batchSplitEnabled, _outputMode);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private — 暂存表检查
    // ═══════════════════════════════════════════════════════════════
    private async Task<PluginResult> CheckStagingTableAsync()
    {
        if (string.IsNullOrEmpty(_targetTable))
            return PluginResult.Fail("目标暂存表未配置", isCritical: true);

        var connStr = DbConnectionsHelper.GetSystemConnectionString(_configuration.GetValue<string>("Security:EncryptionKey"));
        if (string.IsNullOrEmpty(connStr))
            return PluginResult.Fail("未找到系统数据库连接字符串");

        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        StagingTableNameValidator.EnsureSafe(_targetTable);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM sys.tables WHERE name = @name";
        cmd.Parameters.AddWithValue("@name", _targetTable);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        if (count == 0)
            return PluginResult.Fail($"暂存表不存在: [{_targetTable}]，请先创建", isCritical: true);

        return PluginResult.Ok();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private — 读取Excel
    // ═══════════════════════════════════════════════════════════════
    private async Task<List<Dictionary<string, string>>> ReadExcelDataAsync(PluginContext context)
    {
        Stream? localStream = null;
        try
        {
            // 从批次记录获取文件路径
            var batch = await _dbContext.Set<CfBatch>()
                .AsNoTracking()
                .Where(b => b.FID == context.BatchId)
                .Select(b => new { b.FFilePath, b.FFileName })
                .FirstOrDefaultAsync();

            Stream? stream = null;
            // 优先使用 FFileName；为空时从 FFilePath 提取文件名，防止扩展名失配导致解析错误
            string fileName = !string.IsNullOrEmpty(batch?.FFileName)
                ? batch.FFileName
                : !string.IsNullOrEmpty(batch?.FFilePath)
                    ? Path.GetFileName(batch.FFilePath)
                    : "unknown.xlsx";

            if (!string.IsNullOrEmpty(batch?.FFilePath))
            {
                localStream = new FileStream(batch.FFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                stream = localStream;
            }

            if (stream == null)
                return new List<Dictionary<string, string>>();

            var allRows = new List<Dictionary<string, string>>();
            await _excelParser.ParseAsync(
                stream, fileName, _headerRow, _dataStartRow, 5000,
                (batchRows, startRow) =>
                {
                    allRows.AddRange(batchRows);
                    return Task.CompletedTask;
                },
                sheetName: _sheetName);

            _logger.LogInformation("ExcelInputPlugin 读取 {Count} 行数据, 文件={File}", allRows.Count, fileName);
            return allRows;
        }
        finally
        {
            if (localStream != null)
                await localStream.DisposeAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private — DataTable & BulkCopy
    // ═══════════════════════════════════════════════════════════════
    private DataTable CreateDataTable()
    {
        var table = new DataTable();
        table.Columns.Add("F批次ID", typeof(long));
        table.Columns.Add("F原始行号", typeof(int));
        table.Columns.Add("FOrgId", typeof(long));
        table.Columns.Add("F账套ID", typeof(long));

        if (!string.IsNullOrEmpty(_serialNumberRule))
            table.Columns.Add("F流水号", typeof(string));

        foreach (var m in _columnMappings)
        {
            if (_decimalFields.Contains(m.DbColumn))
                table.Columns.Add(m.DbColumn, typeof(decimal));
            else
                table.Columns.Add(m.DbColumn, typeof(string));
        }

        table.Columns.Add("F其他列数据", typeof(string));
        table.Columns.Add("F业务主键", typeof(string));
        return table;
    }

    private async Task BulkCopyAsync(string connStr, string tableName, DataTable dt)
    {
        StagingTableNameValidator.EnsureSafe(tableName);
        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        using var bulkCopy = new SqlBulkCopy(conn);
        bulkCopy.DestinationTableName = $"[{tableName}]";
        bulkCopy.BatchSize = 1000;
        bulkCopy.BulkCopyTimeout = 120;

        bulkCopy.ColumnMappings.Add("F批次ID", "F批次ID");
        bulkCopy.ColumnMappings.Add("F原始行号", "F原始行号");
        bulkCopy.ColumnMappings.Add("FOrgId", "FOrgId");
        bulkCopy.ColumnMappings.Add("F账套ID", "F账套ID");
        if (!string.IsNullOrEmpty(_serialNumberRule))
            bulkCopy.ColumnMappings.Add("F流水号", "F流水号");
        foreach (var m in _columnMappings)
            bulkCopy.ColumnMappings.Add(m.DbColumn, m.DbColumn);
        bulkCopy.ColumnMappings.Add("F其他列数据", "F其他列数据");
        bulkCopy.ColumnMappings.Add("F业务主键", "F业务主键");

        await bulkCopy.WriteToServerAsync(dt);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private — 行级辅助方法
    // ═══════════════════════════════════════════════════════════════
    private HashSet<string> BuildMappedExcelColumnsSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _columnMappings)
        {
            if (!string.IsNullOrEmpty(m.ExcelColumn))
                set.Add(m.ExcelColumn);
            if (m.Aliases != null)
            {
                foreach (var alias in m.Aliases)
                {
                    var trimmed = alias.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        set.Add(trimmed);
                }
            }
        }
        return set;
    }

    private static string GetValueByMapping(Dictionary<string, string> row, ColumnMappingItem mapping)
    {
        if (row.TryGetValue(mapping.ExcelColumn, out var val) && !string.IsNullOrEmpty(val))
            return val;

        if (mapping.Aliases != null)
        {
            foreach (var alias in mapping.Aliases)
            {
                if (row.TryGetValue(alias, out var av) && !string.IsNullOrEmpty(av))
                    return av;
            }
        }
        return string.Empty;
    }

    private string BuildSerialNumber(Dictionary<string, string> row)
    {
        return Regex.Replace(_serialNumberRule!, @"\{(.+?)\}", match =>
        {
            var colName = match.Groups[1].Value;
            if (row.TryGetValue(colName, out var v))
                return v ?? "";
            var mapping = _columnMappings.FirstOrDefault(m => m.DbColumn == colName);
            if (mapping != null)
            {
                if (row.TryGetValue(mapping.ExcelColumn, out var mv))
                    return mv ?? "";
                if (mapping.Aliases != null)
                {
                    foreach (var alias in mapping.Aliases)
                    {
                        if (row.TryGetValue(alias, out var av))
                            return av ?? "";
                    }
                }
            }
            return "";
        });
    }

    private object BuildUnmappedJson(Dictionary<string, string> row, HashSet<string> mappedCols)
    {
        var unmapped = new Dictionary<string, string>();
        foreach (var kv in row)
        {
            if (!mappedCols.Contains(kv.Key) && !string.IsNullOrEmpty(kv.Value))
                unmapped[kv.Key] = kv.Value;
        }
        if (unmapped.Count > 0)
        {
            return JsonSerializer.Serialize(unmapped, new JsonSerializerOptions
            {
                Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        return DBNull.Value;
    }

    private string ComputeBusinessKey(Dictionary<string, string> row)
    {
        var sb = new StringBuilder();
        foreach (var keyField in _keyFields)
        {
            if (row.TryGetValue(keyField, out var kv))
                sb.Append(kv);
        }
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    private bool IsTotalRow(Dictionary<string, string> row)
    {
        if (_totalRowConfig == null) return false;

        if (_totalRowConfig.ContainsKeywords?.Count > 0)
        {
            foreach (var val in row.Values)
            {
                if (string.IsNullOrEmpty(val)) continue;
                foreach (var keyword in _totalRowConfig.ContainsKeywords)
                {
                    if (val.Contains(keyword)) return true;
                }
            }
        }

        if (_totalRowConfig.EmptyFields?.Count > 0)
        {
            bool allEmpty = true;
            foreach (var excelCol in _totalRowConfig.EmptyFields)
            {
                if (row.TryGetValue(excelCol, out var val) && !string.IsNullOrWhiteSpace(val))
                {
                    allEmpty = false;
                    break;
                }
            }
            if (allEmpty) return true;
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private — 跨批次去重查询
    // ═══════════════════════════════════════════════════════════════
    private async Task<HashSet<string>> QueryExistingByFieldsAsync(
        string connStr, string tableName, List<string> fields,
        List<string[]> valueSets, long currentBatchId, long orgId)
    {
        var result = new HashSet<string>();
        if (fields.Count == 0 || valueSets.Count == 0) return result;

        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        var tempTable = $"#dedup_{Guid.NewGuid():N}";
        var fieldDefs = string.Join(", ", fields.Select(f => $"[{f}] NVARCHAR(200)"));

        using (var cmd = new SqlCommand($"CREATE TABLE {tempTable} ({fieldDefs})", conn))
            await cmd.ExecuteNonQueryAsync();

        // 按 fields.Count 动态装配候选值（逐列填充、完整复合键去重）——
        // 修复旧版只填前两列、第三列恒 NULL 导致 JOIN 永不命中的根因。
        var insertDt = CrossBatchDedupKeyBuilder.BuildCandidateTable(fields, valueSets);

        using (var bulkCopy = new SqlBulkCopy(conn))
        {
            bulkCopy.DestinationTableName = tempTable;
            await bulkCopy.WriteToServerAsync(insertDt);
        }

        var joinConditions = string.Join(" AND ", fields.Select(f => $"t.[{f}] = s.[{f}]"));
        // 取临时表侧（已 Trim 的候选值）而非 s.[f]：JOIN 命中与否由库列排序规则决定，
        // 但旧版回 s.[f]（库侧原值）后，内存用序数 Contains 比对会因大小写/尾空格差异
        // 漏判已存在行——取 t.[f] 让 SELECT 回的键与内存比对键逐字一致，消除该不对称。
        var selectFields = string.Join(", ", fields.Select(f => $"t.[{f}]"));

        var querySql = $@"
            SELECT DISTINCT {selectFields}
            FROM [{tableName}] s
            INNER JOIN {tempTable} t ON {joinConditions}
            WHERE s.[FIsRevoked] = 0
              AND s.[FOrgId] = @orgId";

        using (var cmd = new SqlCommand(querySql, conn))
        {
            cmd.Parameters.AddWithValue("@orgId", orgId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var values = new string[fields.Count];
                for (int i = 0; i < fields.Count; i++)
                    values[i] = reader.IsDBNull(i) ? "" : reader.GetString(i);
                result.Add(CrossBatchDedupKeyBuilder.BuildKey(values));
            }
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private — 批次统计更新
    // ═══════════════════════════════════════════════════════════════
    private async Task UpdateBatchStatsAsync(long batchId, string targetTable, int successRows, int failRows, int skippedRows)
    {
        var batch = await _dbContext.Set<CfBatch>()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId);
        if (batch != null)
        {
            batch.FTotalRows = successRows + failRows + skippedRows;
            batch.FSuccessRows = successRows;
            batch.FFailedRows = failRows;
            batch.FSkipRows = skippedRows;
            batch.FActualTargetTable = targetTable;
            batch.FStatus = CfBatchStatus.Parsing; // StagingWrite 阶段对应 Parsing
            await _dbContext.SaveChangesAsync();
        }
    }

    private static string? GetStringProp(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "从 Excel/CSV 文件读取数据写入暂存表";
        return metadata;
    }

    /// <summary>批次拆分结果</summary>
    private class BatchSplitResult
    {
        public string SplitKey { get; set; } = string.Empty;
        public string TargetTable { get; set; } = string.Empty;
        public List<Dictionary<string, object>> Rows { get; set; } = new();
    }
}
