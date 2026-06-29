using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Import;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.CardFlow.Services.Quality;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>质量分析插件 - 对暂存表数据执行质量规则检查（配置驱动，无规则时跳过检查）</summary>
public class QualityAnalysisPlugin : BatchPluginBase
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IQualityDispatchService _qualityDispatchService;
    private readonly IPluginProgressReporter _progressReporter;
    private readonly IQualityRuleEngine _ruleEngine;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QualityAnalysisPlugin> _logger;

    private const decimal DefaultMaxAmountThreshold = 1000000m;
    private const decimal DefaultMinAmountThreshold = -1000000m;

    public QualityAnalysisPlugin(
        STOTOPDbContext dbContext,
        IQualityDispatchService qualityDispatchService,
        IPluginProgressReporter progressReporter,
        IQualityRuleEngine ruleEngine,
        IServiceProvider serviceProvider,
        ILogger<QualityAnalysisPlugin> logger)
    {
        _dbContext = dbContext;
        _qualityDispatchService = qualityDispatchService;
        _progressReporter = progressReporter;
        _ruleEngine = ruleEngine;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override string PluginName => "QualityAnalysis";
    public override string DisplayName => "质量分析";

    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        const int totalSteps = 3;
        try
        {
            // 获取暂存表名
            var targetTable = await GetTargetTableAsync(context);
            if (string.IsNullOrEmpty(targetTable))
            {
                return PluginResult.Ok(); // 未指定暂存表，跳过质量分析
            }

            // ========== Step 1: 加载规则 ==========
            await _progressReporter.ReportProgressAsync(context.BatchId, 0, totalSteps, "加载规则");

            var orgId = await GetOrgIdAsync(context);

            var configuredRules = await _ruleEngine.LoadRulesAsync(targetTable, orgId);

            _logger.LogInformation("质量分析 Step1: 批次 {BatchId}，暂存表 {TargetTable}，加载到 {RuleCount} 条配置规则",
                context.BatchId, targetTable, configuredRules.Count);

            // 无配置规则时，不做任何检查，直接返回成功
            if (configuredRules.Count == 0)
            {
                _logger.LogInformation("质量分析：无配置规则，跳过质量检查，批次 {BatchId}", context.BatchId);
                await _progressReporter.ReportProgressAsync(context.BatchId, 3, totalSteps, "跳过质量检查");
                return new PluginResult
                {
                    Success = true,
                    IsCritical = false,
                    Message = "质量分析：无配置规则，跳过质量检查",
                    ProcessedRows = 0,
                    FailedRows = 0
                };
            }

            // 从 CfPluginRule 读取阈值配置
            var maxAmount = DefaultMaxAmountThreshold;
            var minAmount = DefaultMinAmountThreshold;
            var config = await LoadPluginConfigAsync(context);
            if (config != null)
            {
                var root = config.RootElement;
                if (root.TryGetProperty("maxAmountThreshold", out var maxProp) && maxProp.TryGetDecimal(out var maxVal))
                    maxAmount = maxVal;
                if (root.TryGetProperty("minAmountThreshold", out var minProp) && minProp.TryGetDecimal(out var minVal))
                    minAmount = minVal;
            }

            // 1. 读取批次对应的暂存数据
            var stagingData = await GetStagingDataAsync(targetTable, context.BatchId);

            if (stagingData.Count == 0)
            {
                return PluginResult.Ok(); // 暂存表无数据，跳过
            }

            _logger.LogInformation("质量分析开始：批次 {BatchId}，暂存表 {TargetTable}，共 {Count} 条记录",
                context.BatchId, targetTable, stagingData.Count);

            await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "加载规则");

            // ========== Step 2: 执行检查 ==========
            await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "执行检查");

            var allViolations = new List<QualityViolation>();
            var processedCount = stagingData.Count;

            // === 使用配置规则引擎 ===
            var fieldRowRules = configuredRules.Where(r => r.RuleLevel != "Batch").ToList();
            for (var rowIdx = 0; rowIdx < stagingData.Count; rowIdx++)
            {
                var rowViolations = _ruleEngine.ValidateRow(stagingData[rowIdx], rowIdx, fieldRowRules);
                if (rowViolations.Count > 0 && stagingData[rowIdx].TryGetValue("FID", out var fidVal) && fidVal != null)
                {
                    var stagingId = Convert.ToInt64(fidVal);
                    foreach (var v in rowViolations) v.StagingId = stagingId;
                }
                allViolations.AddRange(rowViolations);
            }

            var batchRules = configuredRules.Where(r => r.RuleLevel == "Batch").ToList();
            if (batchRules.Count > 0)
            {
                var batchViolations = await _ruleEngine.ValidateBatchAsync(targetTable, context.BatchId, batchRules, _dbContext);
                allViolations.AddRange(batchViolations);
            }

            // 统计
            var violatedRowIndices = allViolations.Select(v => v.RowIndex).Where(i => i >= 0).Distinct().Count();
            var batchLevelCount = allViolations.Count(v => v.RowIndex < 0);
            var failCount = violatedRowIndices + (batchLevelCount > 0 ? 1 : 0);
            var passCount = processedCount - violatedRowIndices;

            await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "执行检查");

            // ========== Step 3: 生成报告 ==========
            await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "生成报告");

            // 将 violations 转为 CfBatchError 记录并批量写入
            var errorRecords = new List<CfBatchError>();
            foreach (var violation in allViolations)
            {
                var rowNumber = violation.RowIndex >= 0
                    ? (stagingData.Count > violation.RowIndex && stagingData[violation.RowIndex].TryGetValue("F原始行号", out var rn)
                        ? Convert.ToInt32(rn)
                        : violation.RowIndex + 1)
                    : 0;

                errorRecords.Add(new CfBatchError
                {
                    FBatchId = context.BatchId,
                    FStagingId = violation.StagingId,
                    FRowNumber = rowNumber,
                    FErrorType = violation.ErrorTypeCode,
                    FSeverityLevel = violation.Severity,
                    FErrorField = violation.TargetField,
                    FErrorMessage = violation.ErrorMessage,
                    FSuggestedFix = violation.SuggestedFix,
                    FOriginalValue = violation.OriginalValue,
                    FQualityDimension = violation.QualityDimension,
                    FCreatedTime = DateTime.Now,
                    FDispatchStatus = violation.IsBlocking ? "Pending" : null,
                    FIssueType = violation.ErrorTypeCode,
                    FOrgId = orgId
                });
            }

            if (errorRecords.Count > 0)
            {
                var bulkInsert = context.Services.GetRequiredService<IBulkInsertService>();
                await bulkInsert.BulkInsertErrorsAsync(errorRecords, context.CancellationToken);
            }

            var hasBlockingViolations = allViolations.Any(v => v.IsBlocking);

            // 阻断级错误 → 通过 IIssueAggregator 生成 WorkItem
            if (hasBlockingViolations)
            {
                await AggregateBlockingIssuesAsync(context, orgId, allViolations.Where(v => v.IsBlocking).ToList());
            }

            // 通过 IQualityDispatchService 统一派发质量问题
            var dispatchedCount = 0;
            var errorLevelErrors = errorRecords.Where(e => e.FSeverityLevel == "Error" || e.FSeverityLevel == "ERROR").ToList();

            if (errorLevelErrors.Count > 0)
            {
                try
                {
                    await _qualityDispatchService.DispatchAsync(context.BatchId, orgId, errorLevelErrors);
                    dispatchedCount = errorLevelErrors.Count;

                    _logger.LogInformation("质量分析自动派发：批次 {BatchId}，派发 {Count} 条异常",
                        context.BatchId, dispatchedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "质量分析自动派发失败：批次 {BatchId}", context.BatchId);
                }
            }

            _logger.LogInformation("质量分析完成：批次 {BatchId}，检查 {Total} 条，通过 {Pass} 条，失败 {Fail} 条，阻断={Blocking}",
                context.BatchId, processedCount, passCount, failCount, hasBlockingViolations);

            await _progressReporter.ReportProgressAsync(context.BatchId, 3, totalSteps, "生成报告");

            return new PluginResult
            {
                Success = true,
                IsCritical = hasBlockingViolations,
                Message = failCount > 0
                    ? $"质量分析完成：发现 {failCount} 条问题记录（阻断={hasBlockingViolations}，已派发 {dispatchedCount} 条）"
                    : "质量分析完成：无问题",
                ProcessedRows = processedCount,
                FailedRows = failCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "质量分析Plugin异常：批次 {BatchId}", context.BatchId);
            return PluginResult.Ok($"质量分析阶段异常：{ex.Message}");
        }
    }

    public override async Task RollbackAsync(PluginContext context)
    {
        try
        {
            var errorRecords = await _dbContext.Set<CfBatchError>()
                .Where(e => e.FBatchId == context.BatchId)
                .ToListAsync();

            if (errorRecords.Count > 0)
            {
                _dbContext.Set<CfBatchError>().RemoveRange(errorRecords);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("质量分析回撤：批次 {BatchId}，清除 {Count} 条错误记录",
                    context.BatchId, errorRecords.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "质量分析回撤失败：批次 {BatchId}", context.BatchId);
        }
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "对暂存数据进行质量分析，检测异常值和数据完整性（配置驱动，无规则时跳过）";
        return metadata;
    }

    #region 私有方法

    private async Task<string?> GetTargetTableAsync(PluginContext context)
    {
        var batch = await _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FID == context.BatchId)
            .Select(b => b.FActualTargetTable)
            .FirstOrDefaultAsync();
        return batch;
    }

    private async Task<long> GetOrgIdAsync(PluginContext context)
    {
        var batch = await _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FID == context.BatchId)
            .Select(b => b.FOrgId)
            .FirstOrDefaultAsync();
        return batch;
    }

    private async Task<JsonDocument?> LoadPluginConfigAsync(PluginContext context)
    {
        if (!context.PluginRuleId.HasValue) return null;
        var rule = await _dbContext.Set<CfPluginRule>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FID == context.PluginRuleId.Value);
        if (rule?.F规则配置JSON == null) return null;
        return JsonDocument.Parse(rule.F规则配置JSON);
    }

    private async Task AggregateBlockingIssuesAsync(PluginContext context, long orgId, List<QualityViolation> blockingViolations)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var aggregator = scope.ServiceProvider.GetRequiredService<IIssueAggregator>();

            var groups = blockingViolations.GroupBy(v => v.ErrorTypeCode);
            foreach (var group in groups)
            {
                var issues = group.Select(v => new IssueItem
                {
                    RowId = v.StagingId,
                    ErrorType = v.ErrorTypeCode,
                    ErrorMessage = v.ErrorMessage,
                    FieldName = v.TargetField,
                    OriginalValue = v.OriginalValue
                }).ToList();

                await aggregator.AggregateAsync(new AggregateIssuesRequest
                {
                    OrgId = orgId,
                    DataScopeId = context.BatchId.ToString(),
                    BatchId = context.BatchId,
                    IssueType = group.Key,
                    Issues = issues,
                    AutoCreateWorkItem = true,
                    CreatorId = null
                });
            }

            _logger.LogInformation("质量分析阻断汇报：批次 {BatchId}，{Count} 条阻断问题已提交 IssueAggregator",
                context.BatchId, blockingViolations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "质量分析阻断汇报失败：批次 {BatchId}", context.BatchId);
        }
    }

    private async Task<List<Dictionary<string, object?>>> GetStagingDataAsync(string targetTable, long batchId)
    {
        var conn = _dbContext.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        var sql = $"SELECT * FROM [{targetTable}] WHERE [F批次ID] = @batchId ORDER BY [F原始行号]";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var param = cmd.CreateParameter();
        param.ParameterName = "@batchId";
        param.Value = batchId;
        cmd.Parameters.Add(param);

        var items = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var dict = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            items.Add(dict);
        }

        return items;
    }

    private static bool IsEmptyValue(object? value)
    {
        if (value == null) return true;
        if (value == DBNull.Value) return true;
        return string.IsNullOrWhiteSpace(value.ToString());
    }

    private static bool LooksLikeDateField(string fieldName)
    {
        var lowerName = fieldName.ToLower();
        return lowerName.Contains("日期") || lowerName.Contains("时间") ||
               lowerName.Contains("date") || lowerName.Contains("time");
    }

    private static bool IsValidDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var formats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd",
            "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm:ss",
            "yyyy年MM月dd日", "MM月dd日", "yyyyMMdd",
            "dd/MM/yyyy", "MM/dd/yyyy"
        };
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(value, format, null, DateTimeStyles.None, out _))
                return true;
        }
        return DateTime.TryParse(value, out _);
    }

    #endregion

    /// <summary>质量错误记录（内置兜底规则产出）</summary>
    private class QualityErrorRecord
    {
        public int RowNumber { get; set; }
        public string ErrorType { get; set; } = string.Empty;
        public string SeverityLevel { get; set; } = "WARNING";
        public string? ErrorField { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? SuggestedFix { get; set; }
        public string? OriginalValue { get; set; }
        public string QualityDimension { get; set; } = "完整性";
    }
}
