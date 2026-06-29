using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.Express.Services.Billing;
using STOTOP.Module.System.Services;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 成本计算Plugin：读取已计费成功的运单（F计算状态=1），调用 CostEngine 计算成本
/// 在管道中位于 PricingPlugin 之后执行
/// 通过计费结果表的批次和组织字段过滤，避免扫描全部历史记录
/// </summary>
public class CostPlugin : BatchPluginBase, IQualityIssueTypeProvider
{
    private readonly CostEngine _costEngine;
    private readonly IAutoPluginProgressReporter _progressReporter;
    private readonly IProcessingIssueService _processingIssueService;
    private readonly string _connectionString;
    private readonly ILogger<CostPlugin> _logger;

    public override string PluginName => "Cost";
    public override string DisplayName => "出港运单成本计算";

    public CostPlugin(
        CostEngine costEngine,
        IAutoPluginProgressReporter progressReporter,
        IProcessingIssueService processingIssueService,
        IConfiguration configuration,
        ILogger<CostPlugin> logger)
    {
        _costEngine = costEngine;
        _progressReporter = progressReporter;
        _processingIssueService = processingIssueService;
        _connectionString = DbConnectionsHelper.GetSystemConnectionString(configuration.GetValue<string>("Security:EncryptionKey"))
            ?? throw new InvalidOperationException("无法获取数据库连接字符串");
        _logger = logger;
    }

    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        try
        {
            _logger.LogWarning("CostPlugin: 开始执行，批次ID={BatchId}, OrgId={OrgId}", context.BatchId, context.OrgId);
            await _progressReporter.ReportPhaseAsync(context.BatchId, "数据加载", "开始");

            // 1. 加载配置：优先从 context.ConfigJson，否则从 CfPluginRule 表
            JsonDocument? config = null;
            if (!string.IsNullOrWhiteSpace(context.ConfigJson))
            {
                config = JsonDocument.Parse(context.ConfigJson);
            }
            else if (context.PluginRuleId.HasValue)
            {
                var db = context.Services.GetRequiredService<STOTOPDbContext>();
                var rule = await db.Set<CfPluginRule>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.FID == context.PluginRuleId.Value, context.CancellationToken);

                if (rule?.F规则配置JSON != null)
                    config = JsonDocument.Parse(rule.F规则配置JSON);
            }

            if (config == null)
                return PluginResult.Fail("插件配置为空，请先配置计费结果表");

            // 2. 从配置JSON解析 resultTable 和 sourceTable
            var resultTable = config.RootElement.TryGetProperty("resultTable", out var resultTableProp)
                && resultTableProp.ValueKind == JsonValueKind.String
                ? resultTableProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(resultTable))
                return PluginResult.Fail("未配置计费结果表（resultTable）");

            ValidateTableName(resultTable);

            var sourceTable = config.RootElement.TryGetProperty("sourceTable", out var sourceTableProp)
                && sourceTableProp.ValueKind == JsonValueKind.String
                ? sourceTableProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(sourceTable))
                return PluginResult.Fail("未配置数据源暂存表（sourceTable），无法按批次过滤");

            ValidateTableName(sourceTable);

            var batchId = context.BatchId;
            var orgId = context.OrgId;

            // 3. 从计费结果表读取已计费成功的应收记录。
            var waybills = await ReadSuccessBillingResults(resultTable, sourceTable, batchId, orgId);

            _logger.LogInformation("CostPlugin: 批次 {BatchId} 从 {Table} 读取 {Count} 条已计费成功运单",
                batchId, resultTable, waybills.Count);

            if (waybills.Count == 0)
            {
                var diag = await GetBillingInputDiagnostics(resultTable, batchId, orgId);
                await ReportIssueAsync(batchId, orgId, "ERR_COST_NO_PRICE_RESULT", "Error",
                    message: $"成本计算未找到可处理的计费结果：结果表={resultTable}, 批次计费结果={diag.TotalRows}条, 成功={diag.SuccessRows}条, 应收成功={diag.ReceivableSuccessRows}条",
                    suggestedFix: "请先修复价格计算结果，确认价格插件 resultTable 与成本插件一致后重跑成本计算",
                    originalValue: resultTable,
                    dimension: "Cost");
                await DispatchCostIssuesAsync(batchId, orgId);

                return PluginResult.Fail(
                    $"成本计算未找到可处理的计费结果：批次={batchId}, 结果表={resultTable}, " +
                    $"批次计费结果={diag.TotalRows}条, 成功={diag.SuccessRows}条, " +
                    $"应收成功={diag.ReceivableSuccessRows}条。请检查成本插件 resultTable 是否与价格计算插件一致，或确认价格计算已生成成功结果。");
            }

            // 4. 调用 CostEngine
            await _progressReporter.ReportPhaseAsync(context.BatchId, "成本计算", "开始");
            var result = await _costEngine.ExecuteAsync(waybills, resultTable, context.OrgId, context.BatchId);

            _logger.LogInformation("CostPlugin: 成本计算完成，成功{Success}单，失败{Fail}单，明细{Breakdowns}条，耗时{Duration}",
                result.SuccessCount, result.ErrorCount, result.CostBreakdownsCreated, result.Duration);

            // 5. 构建返回结果
            if (result.ErrorCount > 0)
            {
                if (result.Errors is { Count: > 0 })
                    await ReportCostErrorsAsync(batchId, orgId, result.Errors);

                if (result.Errors is { Count: > 0 })
                {
                    var grouped = result.Errors
                        .GroupBy(e => e.ErrorMessage)
                        .OrderByDescending(g => g.Count())
                        .Select(g => $"{g.Count()}条: {g.First().ErrorMessage} (样例: {g.First().WaybillNo})")
                        .Take(5);
                    _logger.LogWarning("CostPlugin: 成本计算失败原因汇总 -> {Summary}", string.Join(" | ", grouped));
                }

                await DispatchCostIssuesAsync(batchId, orgId);
	
                return new PluginResult
                {
                    Success = false,
                    IsCritical = true,
                    Message = $"成本计算完成: 总计{result.TotalWaybills}单, 成功{result.SuccessCount}单, 失败{result.ErrorCount}单",
                    ProcessedRows = result.SuccessCount,
                    FailedRows = result.ErrorCount
                };
            }

            return PluginResult.Ok(
                $"成本计算完成: 总计{result.TotalWaybills}单, 成功{result.SuccessCount}单",
                result.SuccessCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CostPlugin: 成本计算处理异常，批次={BatchId}", context.BatchId);
            return PluginResult.Fail($"成本计算处理异常: {ex.Message}");
        }
    }

    public override Task RollbackAsync(PluginContext context)
    {
        _logger.LogInformation("CostPlugin: 回撤完成，批次={BatchId}", context.BatchId);
        return Task.CompletedTask;
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "对已计费成功的运单执行成本计算，生成成本明细";
        metadata.ConfigSchema = new List<object>
        {
            new
            {
                key = "sourceTable",
                label = "数据源暂存表",
                fieldType = "custom",
                component = "tableSelect",
                required = true,
                placeholder = "请选择暂存表（STG前缀表）",
                description = "选择数据源暂存表，用于按批次过滤已计费成功的运单"
            },
            new
            {
                key = "resultTable",
                label = "计费结果表",
                fieldType = "custom",
                component = "tableSelect",
                required = true,
                placeholder = "请选择计费结果表",
                description = "选择已有的计费结果表（PricingPlugin 写入的目标表），成本明细将写入对应的 _明细 后缀表",
                extra = JsonSerializer.Serialize(new { tablePrefix = "EXP出港运单_计费结果" })
            }
        };
        return metadata;
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 验证表名合法性（防 SQL 注入）
    /// </summary>
    private static void ValidateTableName(string tableName)
    {
        if (string.IsNullOrEmpty(tableName) ||
            !Regex.IsMatch(tableName, @"^[A-Za-z0-9\u4e00-\u9fff_]+$"))
            throw new ArgumentException($"非法表名: {tableName}");
    }

    /// <summary>
    /// 从计费结果表读取已计费成功的应收运单，构建 CostWaybillInput 列表。
    /// 查询条件：F计算状态=1 AND F参与方角色=1。
    /// 通过 F批次ID + F组织ID 过滤，仅处理当前批次和组织的运单。
    /// </summary>
    private async Task<List<CostWaybillInput>> ReadSuccessBillingResults(string resultTable, string sourceTable, long batchId, long orgId)
    {
        ValidateTableName(resultTable);
        ValidateTableName(sourceTable);

        var sourceColumns = await GetTableColumns(sourceTable);
        var citySelect = sourceColumns.Contains("F目的城市")
            ? "s.[F目的城市] AS [F目的城市]"
            : "NULL AS [F目的城市]";
        var shopSelect = sourceColumns.Contains("F店铺账号")
            ? "s.[F店铺账号] AS [F店铺账号]"
            : "NULL AS [F店铺账号]";
        var revokedPredicate = sourceColumns.Contains("FIsRevoked")
            ? "              AND ISNULL(s.[FIsRevoked], 0) = 0"
            : "";

        var sql = $@"
            SELECT 
                r.FID, r.[F运单编号], r.[F品牌编码],
                r.[F计费重量], r.[F运单日期], r.[F目的省份ID],
                r.[F归属网点编号],
                ISNULL(np.[F组织ID], 0) AS [F归属网点ID],
                {citySelect},
                {shopSelect}
            FROM [{resultTable}] r
            LEFT JOIN [{sourceTable}] s
              ON s.[F运单编号] = r.[F运单编号]
             AND s.[F批次ID] = @batchId
             AND s.[FOrgId] = @orgId
{revokedPredicate}
            LEFT JOIN [EXP快递网点] np
              ON np.[F编号] = r.[F归属网点编号]
             AND (np.[F所属组织ID] = @orgId OR np.[F组织ID] = @orgId)
            WHERE r.[F计算状态] = 1
              AND r.[F参与方角色] = 1
              AND r.[F批次ID] = @batchId
              AND r.[F组织ID] = @orgId";

        var waybills = new List<CostWaybillInput>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@batchId", batchId);
        cmd.Parameters.AddWithValue("@orgId", orgId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            waybills.Add(new CostWaybillInput
            {
                RowId = reader.GetInt64(reader.GetOrdinal("FID")),
                WaybillNo = reader.IsDBNull(reader.GetOrdinal("F运单编号")) ? string.Empty : reader.GetString(reader.GetOrdinal("F运单编号")),
                NetworkPointId = reader.IsDBNull(reader.GetOrdinal("F归属网点ID")) ? 0 : reader.GetInt64(reader.GetOrdinal("F归属网点ID")),
                BrandCode = reader.IsDBNull(reader.GetOrdinal("F品牌编码")) ? string.Empty : reader.GetString(reader.GetOrdinal("F品牌编码")),
                DestinationProvinceId = reader.IsDBNull(reader.GetOrdinal("F目的省份ID")) ? 0 : reader.GetInt32(reader.GetOrdinal("F目的省份ID")),
                DestinationCityName = reader.IsDBNull(reader.GetOrdinal("F目的城市")) ? null : reader.GetString(reader.GetOrdinal("F目的城市")),
                BillableWeight = reader.IsDBNull(reader.GetOrdinal("F计费重量")) ? 0 : reader.GetDecimal(reader.GetOrdinal("F计费重量")),
                WaybillDate = reader.GetDateTime(reader.GetOrdinal("F运单日期")),
                ShopName = reader.IsDBNull(reader.GetOrdinal("F店铺账号")) ? null : reader.GetString(reader.GetOrdinal("F店铺账号"))
            });
        }

        return waybills;
    }

    private async Task<HashSet<string>> GetTableColumns(string tableName)
    {
        ValidateTableName(tableName);

        const string sql = @"
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_NAME] = @tableName";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@tableName", tableName);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private async Task<BillingInputDiagnostics> GetBillingInputDiagnostics(string resultTable, long batchId, long orgId)
    {
        ValidateTableName(resultTable);

        var sql = $@"
            SELECT
                COUNT(1) AS TotalRows,
                SUM(CASE WHEN [F计算状态] = 1 THEN 1 ELSE 0 END) AS SuccessRows,
                SUM(CASE WHEN [F计算状态] = 1
                           AND [F参与方角色] = 1 THEN 1 ELSE 0 END) AS ReceivableSuccessRows
            FROM [{resultTable}]
            WHERE [F批次ID] = @batchId
              AND [F组织ID] = @orgId";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@batchId", batchId);
        cmd.Parameters.AddWithValue("@orgId", orgId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return new BillingInputDiagnostics();

        return new BillingInputDiagnostics
        {
            TotalRows = reader.IsDBNull(reader.GetOrdinal("TotalRows")) ? 0 : Convert.ToInt32(reader["TotalRows"]),
            SuccessRows = reader.IsDBNull(reader.GetOrdinal("SuccessRows")) ? 0 : Convert.ToInt32(reader["SuccessRows"]),
            ReceivableSuccessRows = reader.IsDBNull(reader.GetOrdinal("ReceivableSuccessRows")) ? 0 : Convert.ToInt32(reader["ReceivableSuccessRows"])
        };
    }

    private sealed class BillingInputDiagnostics
    {
        public int TotalRows { get; set; }
        public int SuccessRows { get; set; }
        public int ReceivableSuccessRows { get; set; }
    }

    private async Task ReportCostErrorsAsync(long batchId, long orgId, IReadOnlyList<CostError> errors)
    {
        foreach (var group in errors.GroupBy(e => MapCostErrorCode(e.ErrorMessage)))
        {
            await DeleteExistingErrorsAsync(batchId, group.Key);
            foreach (var error in group)
            {
                await ReportIssueAsync(batchId, orgId, group.Key, "Error",
                    stagingId: error.WaybillId > 0 ? error.WaybillId : null,
                    message: $"运单 {error.WaybillNo} 成本计算失败：{error.ErrorMessage}",
                    suggestedFix: GetCostSuggestedFix(group.Key),
                    originalValue: error.WaybillNo,
                    dimension: "Cost");
            }
        }
    }

    private async Task ReportIssueAsync(
        long batchId,
        long orgId,
        string errorType,
        string severity,
        long? stagingId = null,
        string? message = null,
        string? suggestedFix = null,
        string? originalValue = null,
        string? dimension = null)
    {
        await _processingIssueService.ReportAsync(new ProcessingIssueReportRequest
        {
            BatchId = batchId,
            StagingId = stagingId,
            RowNumber = 0,
            ErrorType = errorType,
            SeverityLevel = severity,
            ErrorField = "成本计算",
            ErrorMessage = message,
            SuggestedFix = suggestedFix,
            OriginalValue = originalValue,
            QualityDimension = dimension
        }, orgId);
    }

    private async Task DispatchCostIssuesAsync(long batchId, long orgId)
    {
        try
        {
            await _processingIssueService.DispatchBatchAsync(batchId, orgId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CostPlugin: 批次 {BatchId} 成本异常派发失败（不影响成本计算结果）", batchId);
        }
    }

    private async Task DeleteExistingErrorsAsync(long batchId, string errorType)
    {
        const string sql = "DELETE FROM [CF批次错误] WHERE [F批次ID] = @batchId AND [F错误类型] = @errorType";
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@batchId", batchId);
        cmd.Parameters.AddWithValue("@errorType", errorType);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string MapCostErrorCode(string message)
    {
        if (message.Contains("未找到启用中的成本方案") || message.Contains("成本矩阵"))
            return "ERR_COST_PLAN_EMPTY";
        if (message.Contains("未匹配到成本项"))
            return "ERR_COST_NO_ITEM";
        return "ERR_COST_UNKNOWN";
    }

    private static string GetCostSuggestedFix(string errorCode) => errorCode switch
    {
        "ERR_COST_NO_PRICE_RESULT" => "请先修复价格计算结果，再重跑成本计算",
        "ERR_COST_PLAN_EMPTY" => "请启用成本方案并维护成本项矩阵",
        "ERR_COST_NO_ITEM" => "请检查成本方案是否覆盖品牌、网点、省份、重量段和生效日期",
        _ => "请检查成本方案配置和运单数据后重跑成本计算"
    };

    public IEnumerable<QualityIssueTypeDefinition> GetIssueTypeDefinitions()
    {
        yield return new QualityIssueTypeDefinition(
            Code: "ERR_COST_NO_PRICE_RESULT",
            Name: "无可用价格结果",
            Module: "Express",
            Category: "Cost",
            SeverityLevel: "Error",
            DetailRoute: "/cardflow/issues",
            SourceAutoPlugin: "CostPlugin",
            Description: "成本计算未找到可处理的成功价格结果",
            SuggestedFix: "请先修复价格计算结果，再重跑成本计算"
        );

        yield return new QualityIssueTypeDefinition(
            Code: "ERR_COST_PLAN_EMPTY",
            Name: "成本方案未启用",
            Module: "Express",
            Category: "Cost",
            SeverityLevel: "Error",
            DetailRoute: "/express/cost-plan",
            SourceAutoPlugin: "CostPlugin",
            Description: "当前组织没有启用的成本方案或成本矩阵为空",
            SuggestedFix: "请启用成本方案并维护成本项矩阵"
        );

        yield return new QualityIssueTypeDefinition(
            Code: "ERR_COST_NO_ITEM",
            Name: "未匹配成本项",
            Module: "Express",
            Category: "Cost",
            SeverityLevel: "Error",
            DetailRoute: "/express/cost-plan",
            SourceAutoPlugin: "CostPlugin",
            Description: "成本方案未覆盖该运单的品牌、网点、省份、重量或日期",
            SuggestedFix: "请检查成本方案覆盖范围后重跑成本计算"
        );

        yield return new QualityIssueTypeDefinition(
            Code: "ERR_COST_UNKNOWN",
            Name: "成本计算未知异常",
            Module: "Express",
            Category: "Cost",
            SeverityLevel: "Error",
            DetailRoute: "/cardflow/issues",
            SourceAutoPlugin: "CostPlugin",
            Description: "成本计算过程中发生未分类异常",
            SuggestedFix: "请检查成本方案配置、运单数据和系统日志"
        );
    }
}
