using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Import;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Express.Services.Billing;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.Express.Services.Agents;

/// <summary>
/// 价格计算Plugin：从 STG 暂存表按批次读取数据，通过列映射转为 BillingWaybillData，调用 PricingEngine 执行价格计算
/// </summary>
public class PricingPlugin : BatchPluginBase, IQualityIssueTypeProvider
{
    private const int MaxPricingIssueRepresentativesPerGroup = 200;

    private readonly PricingEngine _pricingEngine;
    private readonly ProvinceCache _provinceCache;
    private readonly IRepository<ExpProvince> _provinceRepo;
    private readonly IRepository<ExpShop> _shopRepo;
    private readonly IRepository<ExpNetworkPoint> _networkPointRepo;
    private readonly IRepository<ExpNetworkPointAlias> _networkPointAliasRepo;
    private readonly IAutoPluginProgressReporter _progressReporter;
    private readonly IProgressNotifier _notifier;
    private readonly IProcessingIssueService _processingIssueService;
    private readonly IBulkInsertService _bulkInsertService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _connectionString;
    private readonly ILogger<PricingPlugin> _logger;

    public override string PluginName => "出港运单价格计算";
    public override string DisplayName => "计费";

    public PricingPlugin(
        PricingEngine pricingEngine,
        ProvinceCache provinceCache,
        IRepository<ExpProvince> provinceRepo,
        IRepository<ExpShop> shopRepo,
        IRepository<ExpNetworkPoint> networkPointRepo,
        IRepository<ExpNetworkPointAlias> networkPointAliasRepo,
        IAutoPluginProgressReporter progressReporter,
        IProgressNotifier notifier,
        IProcessingIssueService processingIssueService,
        IBulkInsertService bulkInsertService,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration,
        ILogger<PricingPlugin> logger)
    {
        _pricingEngine = pricingEngine;
        _provinceCache = provinceCache;
        _provinceRepo = provinceRepo;
        _shopRepo = shopRepo;
        _networkPointRepo = networkPointRepo;
        _networkPointAliasRepo = networkPointAliasRepo;
        _progressReporter = progressReporter;
        _notifier = notifier;
        _processingIssueService = processingIssueService;
        _bulkInsertService = bulkInsertService;
        _serviceScopeFactory = serviceScopeFactory;
        _connectionString = DbConnectionsHelper.GetSystemConnectionString(configuration.GetValue<string>("Security:EncryptionKey"))
            ?? throw new InvalidOperationException("无法获取数据库连接字符串");
        _logger = logger;
    }

    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        try
        {
            // 1. 读取规则配置：优先从 context.ConfigJson，否则从 CfPluginRule 表
            await _progressReporter.ReportPhaseAsync(context.BatchId, "数据加载", "开始");

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
                    .Where(r => r.FID == context.PluginRuleId.Value)
                    .Select(r => r.F规则配置JSON)
                    .FirstOrDefaultAsync(context.CancellationToken);
                if (!string.IsNullOrWhiteSpace(rule))
                    config = JsonDocument.Parse(rule);
            }

            if (config == null)
                return PluginResult.Fail("插件配置为空，请先配置数据源暂存表、品牌和列映射");

            var sourceTable = config.RootElement.TryGetProperty("sourceTable", out var tableProp)
                && tableProp.ValueKind == JsonValueKind.String
                ? tableProp.GetString() : null;

            string brandCode = string.Empty;
            if (config.RootElement.TryGetProperty("brandCode", out var brandProp))
            {
                if (brandProp.ValueKind == JsonValueKind.String)
                    brandCode = brandProp.GetString() ?? string.Empty;
            }

            Dictionary<string, string> columnMapping;
            if (config.RootElement.TryGetProperty("columnMapping", out var mappingProp))
                columnMapping = ParseColumnMapping(mappingProp);
            else
                columnMapping = new Dictionary<string, string>();

            var batchId = context.BatchId;

            // orgId 从管道上下文获取（不再从 JSON 配置读取）
            long orgId = context.OrgId;

            var resultTable = config.RootElement.TryGetProperty("resultTable", out var resultTableProp)
                && resultTableProp.ValueKind == JsonValueKind.String
                ? resultTableProp.GetString() : null;

            // 2. 验证配置
            ValidateConfig(sourceTable, brandCode, columnMapping, resultTable);

            // 3. 构建动态 SQL 从 STG 表读取批次数据
            var sql = BuildSelectSql(sourceTable!, columnMapping, batchId);

            List<Dictionary<string, object>> rows;
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@batchId", batchId);
                cmd.Parameters.AddWithValue("@orgId", orgId);

                rows = new List<Dictionary<string, object>>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var dict = new Dictionary<string, object>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        dict[reader.GetName(i)] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                    }
                    rows.Add(dict);
                }
            }

            _logger.LogInformation("从 {Table} 读取批次 {BatchId} 共 {Count} 条数据",
                sourceTable, batchId, rows.Count);

            if (rows.Count == 0)
            {
                return PluginResult.Ok("无待计费数据", 0);
            }

            // 4. 映射为 BillingWaybillData 列表
            var waybills = MapToBillingData(rows, brandCode, columnMapping);

            // 统一赋值组织ID（来自Plugin规则配置，而非STG行数据）
            foreach (var w in waybills)
                w.OrgId = orgId;

            // 5. 预扫描：识别并落地两类配置缺失
            var preCheck = await PreCheckShopsAsync(waybills, batchId, orgId);

            // 5.5 预扫描：网点名称匹配检查
            await PreCheckNetworkPointsAsync(waybills, batchId, orgId, preCheck, sourceTable!);

            // 收集被阻断的运单ID（空店铺 + 待配置店铺关联运单 + 未识别网点）
            var blockedIds = new HashSet<long>(preCheck.EmptyShopRowIds);
            foreach (var id in preCheck.UnrecognizedNetworkPointRowIds)
                blockedIds.Add(id);
            if (preCheck.PendingShopNames.Count > 0)
            {
                var pendingShopRowIds = waybills
                    .Where(w => !string.IsNullOrWhiteSpace(w.ShopName)
                        && preCheck.PendingShopNames.Contains(w.ShopName.Trim(), StringComparer.OrdinalIgnoreCase))
                    .Select(w => w.RowId);
                foreach (var id in pendingShopRowIds)
                    blockedIds.Add(id);
            }

            // 将阻断运单的 F计算状态 更新为 3（待人工处理）
            if (blockedIds.Count > 0)
            {
                await UpdateStagingStatusAsync(sourceTable!, blockedIds, 3, batchId);
                _logger.LogWarning("PricingPlugin: 批次 {BatchId} 共 {Count} 条运单因数据质量问题被跳过（空店铺{Empty}条, 待配置店铺{Pending}个）",
                    batchId, blockedIds.Count, preCheck.EmptyShopRows, preCheck.PendingShops);
            }

            // 过滤出可处理运单
            var processableWaybills = waybills.Where(w => !blockedIds.Contains(w.RowId)).ToList();
            if (processableWaybills.Count == 0)
            {
                return new PluginResult
                {
                    Success = false,
                    IsCritical = false,
                    Message = "所有运单均存在数据问题，无可计费运单",
                    ProcessedRows = 0,
                    FailedRows = blockedIds.Count
                };
            }

            // 6. 省份名称 → ID 转换
            await _provinceCache.LoadAsync(_provinceRepo);
            var totalWaybills = processableWaybills.Count;
            for (int i = 0; i < processableWaybills.Count; i++)
            {
                var w = processableWaybills[i];
                if (!string.IsNullOrEmpty(w.DestinationProvince))
                    w.DestinationProvinceId = _provinceCache.FindId(w.DestinationProvince);
                await _progressReporter.ReportAsync(batchId, "价格计算", i + 1, totalWaybills);
                await _notifier.NotifyAutoPluginDataProgressAsync(batchId, "价格计算", i + 1, totalWaybills, "数据预检查");
            }

            // 6.5 重试时清除旧计费失败记录
            var retryWaybillNos = processableWaybills
                .Where(w => w.BillingStatus == 2)
                .Select(w => w.WaybillNo)
                .Where(no => !string.IsNullOrWhiteSpace(no))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (retryWaybillNos.Any())
            {
                await DeleteOldBillingResultsAsync(resultTable!, batchId, retryWaybillNos);
                _logger.LogInformation("PricingPlugin: 批次 {BatchId} 清除 {Count} 条旧计费失败记录", batchId, retryWaybillNos.Count);
            }

            // 7. 调用 PricingEngine
            await _progressReporter.ReportPhaseAsync(context.BatchId, "价格计算", "开始");
            _logger.LogWarning("PricingPlugin 诊断: 批次={BatchId}, STG读取={Total}条, 映射={Mapped}条, 预检查排除={Excluded}条, 送入引擎={EngineInput}条",
                batchId, rows.Count, waybills.Count, waybills.Count - processableWaybills.Count, processableWaybills.Count);
            var result = await _pricingEngine.ExecuteAsync(processableWaybills, sourceTable!, batchId, resultTable!, _notifier);

            _logger.LogInformation("PricingPlugin: 价格计算完成，成功{Success}单，失败{Fail}单，耗时{Duration}",
                result.SuccessCount, result.ErrorCount, result.Duration);

            // 7.5 写入网点不一致警告记录（纯警告，不影响成功/失败统计）
            await _progressReporter.ReportPhaseAsync(context.BatchId, "结果写入", "开始");
            await DeleteExistingErrorsAsync(batchId, "ERR_NETWORK_POINT_MISMATCH");
            if (result.NetworkPointMismatches?.Count > 0)
            {
                foreach (var mismatch in result.NetworkPointMismatches)
                {
                    await ReportIssueAsync(batchId, orgId, "ERR_NETWORK_POINT_MISMATCH", "Warning",
                        stagingId: mismatch.RowId,
                        field: "F归属网点编号",
                        message: $"运单 {mismatch.WaybillNo} 映射网点 [{mismatch.WaybillNpCode}] 与报价网点 [{mismatch.QuotationNpCode}] 不一致",
                        originalValue: mismatch.WaybillNpCode,
                        dimension: "Consistency");
                }
                _logger.LogWarning("PricingPlugin: 批次 {BatchId} 发现 {Count} 条网点不一致警告",
                    batchId, result.NetworkPointMismatches.Count);
            }

            // 失败原因聚合统计（按 ErrorCode 分组）
            if (result.ErrorCount > 0 && result.Errors is { Count: > 0 })
            {
                await ReportPricingEngineErrorsAsync(batchId, orgId, result.Errors);

                var grouped = result.Errors
                    .GroupBy(e => e.ErrorCode)
                    .OrderByDescending(g => g.Count())
                    .Select(g => $"{g.Key}={g.Count()}条 (样例: {g.First().WaybillNo} {g.First().ErrorMessage})")
                    .Take(5);
                _logger.LogWarning("PricingPlugin: 价格计算失败原因汇总 -> {Summary}", string.Join(" | ", grouped));
            }

            // 8. 构建返回结果
            if (result.ErrorCount > 0)
            {
                if (result.SuccessCount == 0)
                {
                    var existingSuccessCount = await CountExistingSuccessResultsAsync(resultTable!, batchId, orgId);
                    if (existingSuccessCount > 0)
                    {
                        QueueQualityIssueDispatch(batchId, orgId);
                        return new PluginResult
                        {
                            Success = true,
                            IsCritical = false,
                            Message = $"价格重算未新增成功结果: 本轮{result.TotalWaybills}单全部失败，已存在成功计费结果{existingSuccessCount}条，继续执行成本计算",
                            ProcessedRows = existingSuccessCount,
                            FailedRows = result.ErrorCount
                        };
                    }

                    // 全部失败
                    QueueQualityIssueDispatch(batchId, orgId);
                    return new PluginResult
                    {
                        Success = false,
                        IsCritical = true,
                        Message = $"价格计算全部失败: 总计{result.TotalWaybills}单, 失败{result.ErrorCount}单",
                        ProcessedRows = 0,
                        FailedRows = result.ErrorCount
                    };
                }

                // 部分成功
                QueueQualityIssueDispatch(batchId, orgId);
                return new PluginResult
                {
                    Success = true,
                    IsCritical = false,
                    Message = $"价格计算部分完成: 总计{result.TotalWaybills}单, 成功{result.SuccessCount}单, 失败{result.ErrorCount}单",
                    ProcessedRows = result.SuccessCount,
                    FailedRows = result.ErrorCount
                };
            }

            // 全部成功
            QueueQualityIssueDispatch(batchId, orgId);
            return PluginResult.Ok(
                $"计费完成: 总计{result.TotalWaybills}单, 成功{result.SuccessCount}单",
                result.SuccessCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PricingPlugin: 价格计算处理异常，批次={BatchId}", context.BatchId);
            return PluginResult.Fail($"价格计算处理异常: {ex.Message}");
        }
    }

    private async Task<int> CountExistingSuccessResultsAsync(string resultTable, long batchId, long orgId)
    {
        ValidateTableName(resultTable);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand($@"
SELECT COUNT(1)
FROM [{resultTable}]
WHERE [F批次ID] = @batchId
  AND [F组织ID] = @orgId
  AND [F计算状态] = 1
  AND [F参与方角色] = 1
  AND ISNULL([F层级], 0) = 0", connection);
        cmd.Parameters.AddWithValue("@batchId", batchId);
        cmd.Parameters.AddWithValue("@orgId", orgId);

        var value = await cmd.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    /// <summary>批次处理完成后，统一派发该批次待处理异常</summary>
    private void QueueQualityIssueDispatch(long batchId, long orgId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var issueService = scope.ServiceProvider.GetRequiredService<IProcessingIssueService>();
                await issueService.DispatchBatchAsync(batchId, orgId);
                _logger.LogInformation("PricingPlugin: 批次 {BatchId} 质量问题派发完成", batchId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PricingPlugin: 批次 {BatchId} 质量问题后台派发失败（不影响计费和成本计算）", batchId);
            }
        });
    }

    private async Task ReportPricingEngineErrorsAsync(long batchId, long orgId, IReadOnlyList<BillingError> errors)
    {
        foreach (var group in errors.GroupBy(e => e.ErrorCode))
        {
            await DeleteExistingErrorsAsync(batchId, group.Key);
        }

        var issueRecords = BuildRepresentativePricingIssues(batchId, orgId, errors);
        await _bulkInsertService.BulkInsertErrorsAsync(issueRecords);

        if (issueRecords.Count < errors.Count)
        {
            _logger.LogWarning(
                "PricingPlugin: 批次 {BatchId} 将 {ErrorCount} 条价格失败聚合为 {IssueCount} 条质量问题记录，逐票明细保留在计费结果表",
                batchId, errors.Count, issueRecords.Count);
        }
    }

    private static List<CfBatchError> BuildRepresentativePricingIssues(
        long batchId,
        long orgId,
        IReadOnlyList<BillingError> errors)
    {
        var records = new List<CfBatchError>();
        var now = DateTime.Now;

        foreach (var codeGroup in errors
            .Where(e => !string.IsNullOrWhiteSpace(e.ErrorCode))
            .GroupBy(e => e.ErrorCode.Trim()))
        {
            var signatureGroups = codeGroup
                .GroupBy(e => BuildPricingIssueGroupKey(codeGroup.Key, e))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key.ShopName ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(g => g.Key.NormalizedMessage, StringComparer.Ordinal)
                .ToList();

            foreach (var signatureGroup in signatureGroups.Take(MaxPricingIssueRepresentativesPerGroup))
            {
                var sample = signatureGroup.First();
                var affectedRows = signatureGroup.Count();
                records.Add(CreatePricingIssueRecord(
                    batchId,
                    orgId,
                    codeGroup.Key,
                    sample,
                    affectedRows,
                    signatureGroup.Key.NormalizedMessage,
                    signatureGroup.Key.ShopName,
                    now));
            }

            var omittedRows = signatureGroups
                .Skip(MaxPricingIssueRepresentativesPerGroup)
                .Sum(g => g.Count());
            if (omittedRows > 0)
            {
                var sample = signatureGroups
                    .Skip(MaxPricingIssueRepresentativesPerGroup)
                    .Select(g => g.First())
                    .First();
                records.Add(CreatePricingIssueRecord(
                    batchId,
                    orgId,
                    codeGroup.Key,
                    sample,
                    omittedRows,
                    $"同类异常过多，已省略 {omittedRows} 条代表性展开；请在计费结果表按异常信息查询逐票明细",
                    null,
                    now));
            }
        }

        return records;
    }

    private static PricingIssueGroupKey BuildPricingIssueGroupKey(string errorCode, BillingError error)
    {
        var normalizedMessage = NormalizePricingErrorMessage(error.ErrorMessage);
        if (!IsShopPricePlanMissError(errorCode))
            return new PricingIssueGroupKey(normalizedMessage, null);

        var shopName = NormalizeShopName(error.ShopName) ?? ExtractShopNameFromPricingErrorMessage(normalizedMessage);
        if (string.IsNullOrWhiteSpace(shopName))
            return new PricingIssueGroupKey(normalizedMessage, null);

        return new PricingIssueGroupKey($"店铺 {shopName} 无任何生效报价", shopName);
    }

    private sealed record PricingIssueGroupKey(string NormalizedMessage, string? ShopName);

    private static CfBatchError CreatePricingIssueRecord(
        long batchId,
        long orgId,
        string errorCode,
        BillingError sample,
        int affectedRows,
        string normalizedMessage,
        string? shopName,
        DateTime createdTime)
    {
        var sampleWaybill = string.IsNullOrWhiteSpace(sample.WaybillNo) ? "-" : sample.WaybillNo;
        var normalizedShopName = NormalizeShopName(shopName);
        var isShopPricePlanMiss = IsShopPricePlanMissError(errorCode) && !string.IsNullOrWhiteSpace(normalizedShopName);
        var message = isShopPricePlanMiss
            ? $"店铺 {normalizedShopName} 匹配不到价格 {affectedRows} 条：{normalizedMessage}（样例运单 {sampleWaybill}）"
            : affectedRows > 1
                ? $"价格计算失败 {affectedRows} 条：{normalizedMessage}（样例运单 {sampleWaybill}）"
                : $"运单 {sampleWaybill} 价格计算失败：{normalizedMessage}";

        return new CfBatchError
        {
            FBatchId = batchId,
            FStagingId = sample.WaybillId > 0 ? sample.WaybillId : null,
            FRowNumber = 0,
            FErrorType = TruncateText(errorCode, 50) ?? string.Empty,
            FSeverityLevel = "Error",
            FErrorField = isShopPricePlanMiss ? "F店铺账号" : null,
            FErrorMessage = TruncateText(message, 500),
            FSuggestedFix = TruncateText(GetPricingSuggestedFix(errorCode), 500),
            FOriginalValue = TruncateText(normalizedShopName ?? sampleWaybill, 200),
            FQualityDimension = "Pricing",
            FCreatedTime = createdTime,
            FDispatchStatus = "Pending",
            FIssueType = TruncateText(errorCode, 50) ?? string.Empty,
            FProcessResult = 0,
            FResolutionStatus = "Pending",
            FResolutionPayloadJson = JsonSerializer.Serialize(new
            {
                affectedRows,
                shopName = normalizedShopName,
                sampleWaybillNo = sampleWaybill,
                sampleStagingId = sample.WaybillId,
                errorMessage = normalizedMessage
            }),
            FRetryStatus = "None",
            FOrgId = orgId
        };
    }

    private static string NormalizePricingErrorMessage(string? message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "未知价格计算异常"
            : message.Trim();
    }

    private static bool IsShopPricePlanMissError(string? errorCode)
    {
        return string.Equals(errorCode?.Trim(), "ERR_NO_PRICE_PLAN", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeShopName(string? shopName)
    {
        return string.IsNullOrWhiteSpace(shopName) ? null : shopName.Trim();
    }

    private static string? ExtractShopNameFromPricingErrorMessage(string message)
    {
        const string prefix = "店铺 ";
        const string suffix = " 无任何生效报价";

        if (!message.StartsWith(prefix, StringComparison.Ordinal) || !message.EndsWith(suffix, StringComparison.Ordinal))
            return null;

        var shopName = message[prefix.Length..^suffix.Length].Trim();
        return string.IsNullOrWhiteSpace(shopName) ? null : shopName;
    }

    private static string? TruncateText(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;
        return value[..maxLength];
    }

    private async Task ReportIssueAsync(
        long batchId,
        long orgId,
        string errorType,
        string severity,
        long? stagingId = null,
        string? field = null,
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
            ErrorField = field,
            ErrorMessage = message,
            SuggestedFix = suggestedFix,
            OriginalValue = originalValue,
            QualityDimension = dimension
        }, orgId);
    }

    private static string? GetPricingSuggestedFix(string errorCode) => errorCode switch
    {
        "ERR_NO_WEIGHT" => "请检查源数据重量字段，补全结算重量或相关环节重量后重跑价格计算",
        "ERR_NO_PRICE_CELL" => "请检查报价方案中的省份和重量段矩阵是否覆盖该运单",
        "ERR_NO_PRICE_PLAN" => "请为店铺绑定有效报价方案，或维护对应品牌/客户类型的报价",
        _ => "请检查价格计算配置和源数据后重跑"
    };

    public override Task RollbackAsync(PluginContext context)
    {
        _logger.LogInformation("PricingPlugin: 回撤完成，批次={BatchId}", context.BatchId);
        return Task.CompletedTask;
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "对出港运单执行价格计算";
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
                description = "选择价格计算的数据来源暂存表"
            },
            new
            {
                key = "brandCode",
                label = "快递品牌",
                fieldType = "custom",
                component = "brandSelect",
                required = true,
                description = "选择该暂存表数据所属的快递品牌"
            },
            new
            {
                key = "resultTable",
                label = "结果存储表",
                fieldType = "custom",
                component = "tableSelect",
                required = true,
                placeholder = "请选择计费结果存储表",
                description = "选择计费结果写入的目标表（需与 EXP出港运单_计费结果 表结构一致）",
                extra = JsonSerializer.Serialize(new { tablePrefix = "EXP出港运单_计费结果" })
            },
            new
            {
                key = "columnMapping",
                label = "列映射配置",
                fieldType = "custom",
                component = "columnMapping",
                required = true,
                description = "将暂存表列映射到标准计费字段",
                extra = JsonSerializer.Serialize(new
                {
                    standardFields = new object[]
                    {
                        new { key = "waybillNo", label = "运单编号", required = true },
                        new { key = "shopName", label = "店铺名称", required = true },
                        new { key = "waybillDate", label = "业务日期", required = true },
                        new { key = "billingStatus", label = "计算状态", required = true },
                        new { key = "destinationProvince", label = "目的省份", required = false },
                        new { key = "destinationCity", label = "目的城市", required = false },
                        new { key = "clientAlias", label = "共享别名", required = false },
                        new { key = "settlementWeight", label = "结算重量", required = false },
                        new { key = "pickupWeight", label = "揽收重量", required = false },
                        new { key = "transitWeight", label = "中转重量", required = false },
                        new { key = "deliveryWeight", label = "到件重量", required = false },
                        new { key = "bundleWeight", label = "集包重量", required = false },
                        new { key = "volumeWeight", label = "计泡重量", required = false },
                        new { key = "hqWeight", label = "总部重量", required = false },
                        new { key = "declarationValue", label = "声明价值", required = false },
                        new { key = "networkPointName", label = "所属网点", required = false }
                    }
                })
            }
        };
        return metadata;
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 解析列映射配置 JSON → Dictionary（standardField → stgColumnName）。
    /// internal：PricingExplainProvider 复用同一套配置解析，保证解释与计费口径一致。
    /// </summary>
    internal static Dictionary<string, string> ParseColumnMapping(JsonElement mappingElement)
    {
        var result = new Dictionary<string, string>();

        if (mappingElement.ValueKind == JsonValueKind.String)
        {
            var raw = mappingElement.GetString();
            if (string.IsNullOrWhiteSpace(raw)) return result;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                return ParseColumnMapping(doc.RootElement.Clone());
            }
            catch
            {
                return result;
            }
        }

        if (mappingElement.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var prop in mappingElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(prop.Value.GetString()))
            {
                result[prop.Name] = prop.Value.GetString()!;
            }
        }
        return result;
    }

    /// <summary>
    /// 验证配置完整性
    /// </summary>
    private static void ValidateConfig(string? sourceTable, string brandCode, Dictionary<string, string> columnMapping, string? resultTable)
    {
        if (string.IsNullOrWhiteSpace(sourceTable))
            throw new ArgumentException("未配置数据源暂存表（sourceTable）");

        ValidateTableName(sourceTable);

        if (string.IsNullOrWhiteSpace(resultTable))
            throw new ArgumentException("未配置结果存储表（resultTable）");

        ValidateTableName(resultTable);

        if (string.IsNullOrWhiteSpace(brandCode))
            throw new ArgumentException("未配置快递品牌（brandCode）");

        var requiredFields = new[] { "waybillNo", "shopName", "waybillDate", "billingStatus" };
        foreach (var field in requiredFields)
        {
            if (!columnMapping.ContainsKey(field))
                throw new ArgumentException($"列映射缺少必填字段: {field}");
        }
    }

    /// <summary>
    /// 验证表名合法性（防 SQL 注入）
    /// </summary>
    internal static void ValidateTableName(string tableName)
    {
        if (string.IsNullOrEmpty(tableName) ||
            !Regex.IsMatch(tableName, @"^[A-Za-z0-9\u4e00-\u9fff_]+$"))
            throw new ArgumentException($"非法表名: {tableName}");
    }

    /// <summary>
    /// 根据列映射构建 SELECT SQL
    /// </summary>
    private static string BuildSelectSql(string sourceTable, Dictionary<string, string> mapping, long batchId)
    {
        ValidateTableName(sourceTable);

        var columns = new List<string> { "[FID]" };
        foreach (var kvp in mapping)
        {
            var colName = kvp.Value;
            if (!columns.Contains($"[{colName}]"))
                columns.Add($"[{colName}]");
        }

        return $"SELECT {string.Join(", ", columns)} FROM [{sourceTable}] WHERE [F批次ID] = @batchId AND ([F计算状态] IS NULL OR [F计算状态] IN (0, 2, 3)) AND [FOrgId] = @orgId";
    }

    /// <summary>
    /// 将动态查询结果映射为 BillingWaybillData 列表。
    /// internal：PricingExplainProvider 复用同一套字段映射，保证解释与计费口径一致。
    /// </summary>
    internal static List<BillingWaybillData> MapToBillingData(
        List<Dictionary<string, object>> rows, string brandCode, Dictionary<string, string> mapping)
    {
        var result = new List<BillingWaybillData>(rows.Count);
        foreach (var dict in rows)
        {
            var data = new BillingWaybillData
            {
                RowId = Convert.ToInt64(dict["FID"]),
                BrandCode = brandCode,
                WaybillNo = GetStringValue(dict, mapping, "waybillNo") ?? string.Empty,
                ShopName = GetStringValue(dict, mapping, "shopName") ?? string.Empty,
                WaybillDate = GetDateValue(dict, mapping, "waybillDate") ?? DateTime.MinValue,
                ClientAlias = GetStringValue(dict, mapping, "clientAlias"),
                DestinationProvince = GetStringValue(dict, mapping, "destinationProvince"),
                DestinationCityName = GetStringValue(dict, mapping, "destinationCity"),
                BillingStatus = GetIntValue(dict, mapping, "billingStatus") ?? 0,
                SettlementWeight = GetDecimalValue(dict, mapping, "settlementWeight"),
                PickupWeight = GetDecimalValue(dict, mapping, "pickupWeight"),
                TransitWeight = GetDecimalValue(dict, mapping, "transitWeight"),
                DeliveryWeight = GetDecimalValue(dict, mapping, "deliveryWeight"),
                BundleWeight = GetDecimalValue(dict, mapping, "bundleWeight"),
                VolumeWeight = GetDecimalValue(dict, mapping, "volumeWeight"),
                HqWeight = GetDecimalValue(dict, mapping, "hqWeight"),
                DeclarationValue = GetDecimalValue(dict, mapping, "declarationValue"),
                NetworkPointName = GetStringValue(dict, mapping, "networkPointName"),

            };
            result.Add(data);
        }
        return result;
    }

    private static string? GetStringValue(Dictionary<string, object> dict, Dictionary<string, string> mapping, string fieldKey)
    {
        if (!mapping.TryGetValue(fieldKey, out var colName)) return null;
        if (!dict.TryGetValue(colName, out var val)) return null;
        if (val == DBNull.Value || val == null) return null;
        return val.ToString();
    }

    private static DateTime? GetDateValue(Dictionary<string, object> dict, Dictionary<string, string> mapping, string fieldKey)
    {
        if (!mapping.TryGetValue(fieldKey, out var colName)) return null;
        if (!dict.TryGetValue(colName, out var val)) return null;
        if (val == DBNull.Value || val == null) return null;
        if (val is DateTime dt) return dt;
        if (DateTime.TryParse(val.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static int? GetIntValue(Dictionary<string, object> dict, Dictionary<string, string> mapping, string fieldKey)
    {
        if (!mapping.TryGetValue(fieldKey, out var colName)) return null;
        if (!dict.TryGetValue(colName, out var val)) return null;
        if (val == DBNull.Value || val == null) return null;
        if (val is int i) return i;
        if (int.TryParse(val.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static decimal? GetDecimalValue(Dictionary<string, object> dict, Dictionary<string, string> mapping, string fieldKey)
    {
        if (!mapping.TryGetValue(fieldKey, out var colName)) return null;
        if (!dict.TryGetValue(colName, out var val)) return null;
        if (val == DBNull.Value || val == null) return null;
        if (val is decimal d) return d;
        if (decimal.TryParse(val.ToString(), out var parsed)) return parsed;
        return null;
    }

    // ==================== 预扫描：店铺配置完整性检查 ====================

    private class PreCheckResult
    {
        public int EmptyShopRows { get; set; }
        public int AutoCreatedShops { get; set; }
        public int PendingShops { get; set; }
        public int UnrecognizedNetworkPoints { get; set; }
        public bool HasBlockers => EmptyShopRows > 0 || PendingShops > 0 || UnrecognizedNetworkPoints > 0;
        /// <summary>空店铺账号运单的 RowId 列表</summary>
        public List<long> EmptyShopRowIds { get; set; } = new();
        /// <summary>待配置店铺名称列表（含自动建档 + 已存在但未配置的）</summary>
        public List<string> PendingShopNames { get; set; } = new();
        /// <summary>未识别网点运单的 RowId 列表</summary>
        public List<long> UnrecognizedNetworkPointRowIds { get; set; } = new();
    }

    /// <summary>
    /// 对批次运单执行店铺配置预检
    /// </summary>
    private async Task<PreCheckResult> PreCheckShopsAsync(List<BillingWaybillData> waybills, long batchId, long orgId)
    {
        var r = new PreCheckResult();

        // --- 场景 A：空店铺账号 ---
        var emptyRows = waybills.Where(w => string.IsNullOrWhiteSpace(w.ShopName)).ToList();
        r.EmptyShopRows = emptyRows.Count;
        r.EmptyShopRowIds = emptyRows.Select(w => w.RowId).ToList();
        if (emptyRows.Count > 0)
        {
            await DeleteExistingErrorsAsync(batchId, "ERR_SHOP_EMPTY");

            var sampleCount = 0;
            foreach (var w in emptyRows)
            {
                if (sampleCount >= 200) break;
                await ReportIssueAsync(batchId, orgId, "ERR_SHOP_EMPTY", "Error",
                    stagingId: w.RowId,
                    field: "F店铺账号",
                    message: $"运单 {w.WaybillNo} 店铺账号为空，无法确定业务对象",
                    suggestedFix: "请在「数据质量中心 → 空店铺账号」补填店铺账号或标记忽略计费",
                    dimension: "Completeness");
                sampleCount++;
            }
            _logger.LogWarning("PricingPlugin: 在批次 {BatchId} 发现 {Count} 条空店铺账号运单，已写入 CF批次错误 (最多{Max}条)", batchId, emptyRows.Count, Math.Min(emptyRows.Count, 200));
        }

        // --- 场景 B/C：店铺账号有值，检查 EXP店铺 是否已建档且已配置 ---
            var distinctShops = waybills
            .Where(w => !string.IsNullOrWhiteSpace(w.ShopName))
            .Select(w => w.ShopName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctShops.Count == 0) return r;

        var existingShops = await _shopRepo.Query()
            .Where(s => distinctShops.Contains(s.FName))
            .Select(s => new { s.FName, s.FNeedsAssignment })
            .ToListAsync();

        var existingCodeSet = new HashSet<string>(existingShops.Select(s => s.FName), StringComparer.OrdinalIgnoreCase);
        var missingShops = distinctShops.Where(c => !existingCodeSet.Contains(c)).ToList();

        // 场景 B：自动建档
        r.PendingShopNames.AddRange(missingShops);
        if (missingShops.Count > 0)
        {
            await DeleteExistingErrorsAsync(batchId, "ERR_SHOP_PENDING_CONFIG");

            foreach (var code in missingShops)
            {
                var earliestDate = waybills
                    .Where(w => string.Equals(w.ShopName, code, StringComparison.OrdinalIgnoreCase))
                    .Select(w => w.WaybillDate)
                    .DefaultIfEmpty(DateTime.Now)
                    .Min();

                var shop = new ExpShop
                {
                    FName = code,
                    FIsShared = false,
                    FIsAutoCreated = true,
                    FNeedsAssignment = true,
                    FStatus = 0,
                    FRemark = $"由批次 {batchId} 自动创建，最早运单日期: {earliestDate:yyyy-MM-dd}",
                    FCreatedTime = DateTime.Now,
                    FUpdatedTime = DateTime.Now
                };
                await _shopRepo.AddAsync(shop);
                r.AutoCreatedShops++;

                var affected = waybills.Count(w => string.Equals(w.ShopName, code, StringComparison.OrdinalIgnoreCase));
                await ReportIssueAsync(batchId, orgId, "ERR_SHOP_PENDING_CONFIG", "Warning",
                    field: "F店铺账号",
                    message: $"店铺 [{code}] 已自动建档，等待配置归属客户与报价方案 ({affected} 条运单受影响)",
                    suggestedFix: "请在「数据质量中心 → 待配置店铺」为该店铺选择业务对象并指定报价方案",
                    originalValue: code,
                    dimension: "Integrity");
            }
            _logger.LogWarning("PricingPlugin: 批次 {BatchId} 自动创建 {Count} 个待配置店铺", batchId, missingShops.Count);
        }

        // 场景 C：统计本批次相关的待配置店铺总数
        var existingPendingCount = existingShops.Count(s => s.FNeedsAssignment);
        r.PendingShops = r.AutoCreatedShops + existingPendingCount;

        if (existingPendingCount > 0)
        {
            var pendingExistingCodes = existingShops
                .Where(s => s.FNeedsAssignment)
                .Select(s => s.FName)
                .ToList();
            r.PendingShopNames.AddRange(pendingExistingCodes);
            foreach (var code in pendingExistingCodes)
            {
                var affected = waybills.Count(w => string.Equals(w.ShopName, code, StringComparison.OrdinalIgnoreCase));
                await ReportIssueAsync(batchId, orgId, "ERR_SHOP_PENDING_CONFIG", "Warning",
                    field: "F店铺账号",
                    message: $"店铺 [{code}] 尚未配置归属客户/报价 ({affected} 条运单受影响)",
                    suggestedFix: "请在「数据质量中心 → 待配置店铺」为该店铺选择业务对象并指定报价方案",
                    originalValue: code,
                    dimension: "Integrity");
            }
        }

        return r;
    }

    /// <summary>
    /// 对批次运单执行网点名称匹配检查
    /// </summary>
    private async Task PreCheckNetworkPointsAsync(List<BillingWaybillData> waybills, long batchId, long orgId, PreCheckResult r, string sourceTable)
    {
        // 只对有配置 networkPointName 映射的场景生效
        var hasNpField = waybills.Any(w => w.NetworkPointName != null);
        if (!hasNpField) return;

        // 第1优先：从网点表加载 全称 和 简称
        var networkPoints = await _networkPointRepo.Query()
            .Select(np => new { np.FCode, np.FFullName, np.FShortName })
            .ToListAsync();

        var aliasMap = new Dictionary<string, string>();
        foreach (var np in networkPoints)
        {
            if (!string.IsNullOrEmpty(np.FFullName))
                aliasMap[np.FFullName] = np.FCode;
            if (!string.IsNullOrEmpty(np.FShortName) && !aliasMap.ContainsKey(np.FShortName))
                aliasMap[np.FShortName] = np.FCode;
        }

        // 第2优先：别名表兜底（不覆盖已有的正式名称匹配）
        var aliases = await _networkPointAliasRepo.Query().ToListAsync();
        foreach (var alias in aliases)
        {
            if (!string.IsNullOrEmpty(alias.FName) && !aliasMap.ContainsKey(alias.FName))
                aliasMap[alias.FName] = alias.FNetworkPointCode;
        }

        // 逐条匹配
        var unrecognized = new List<BillingWaybillData>();
        foreach (var waybill in waybills)
        {
            var npName = waybill.NetworkPointName;
            if (string.IsNullOrWhiteSpace(npName))
            {
                unrecognized.Add(waybill);
                continue;
            }
            if (aliasMap.TryGetValue(npName.Trim(), out var code))
            {
                waybill.NetworkPointCode = code;
            }
            else
            {
                unrecognized.Add(waybill);
            }
        }

        if (unrecognized.Count == 0) return;

        // 写入错误记录
        await DeleteExistingErrorsAsync(batchId, "ERR_NETWORK_POINT_NOT_FOUND");

        var sampleCount = 0;
        foreach (var w in unrecognized)
        {
            if (sampleCount >= 200) break;
            await ReportIssueAsync(batchId, orgId, "ERR_NETWORK_POINT_NOT_FOUND", "Error",
                stagingId: w.RowId,
                field: "F所属网点",
                message: $"运单 {w.WaybillNo} 所属网点 [{w.NetworkPointName}] 无法匹配到已有网点",
                suggestedFix: "请在「数据质量中心 → 未识别网点」关联已有网点或新增网点",
                originalValue: w.NetworkPointName ?? "",
                dimension: "Accuracy");
            sampleCount++;
        }

        // 标记暂存表状态为3（待人工处理）
        var blockedIds = new HashSet<long>(unrecognized.Select(w => w.RowId));
        await UpdateStagingStatusAsync(sourceTable, blockedIds, 3, batchId);

        r.UnrecognizedNetworkPoints = unrecognized.Count;
        r.UnrecognizedNetworkPointRowIds = unrecognized.Select(w => w.RowId).ToList();

        _logger.LogWarning("PricingPlugin: 批次 {BatchId} 发现 {Count} 条未识别网点运单，已写入 CF批次错误 (最多{Max}条)",
            batchId, unrecognized.Count, Math.Min(unrecognized.Count, 200));
    }

    /// <summary>批量更新暂存表的计算状态</summary>
    private async Task UpdateStagingStatusAsync(string sourceTable, HashSet<long> waybillIds, int calcStatus, long batchId)
    {
        if (!waybillIds.Any()) return;

        ValidateTableName(sourceTable);
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var batch in waybillIds.Chunk(1000))
        {
            var idList = string.Join(",", batch);
            using var cmd = new SqlCommand(
                $"UPDATE [{sourceTable}] SET [F计算状态] = @status WHERE [FID] IN ({idList}) AND [F批次ID] = @batchId",
                connection);
            cmd.Parameters.AddWithValue("@status", calcStatus);
            cmd.Parameters.AddWithValue("@batchId", batchId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// 删除旧计费结果（重试场景）。
    /// WHERE 限定 [F批次ID]=@batchId 防止跨批次误删（运单号跨批次可重复）；
    /// 不限状态，因为是引擎前预清、无两阶段互删问题。
    /// </summary>
    /// <param name="resultTable">计费结果表名</param>
    /// <param name="batchId">当前批次 ID，DELETE 限定在本批次内</param>
    /// <param name="waybillNos">需要重试的运单编号列表</param>
    private async Task DeleteOldBillingResultsAsync(string resultTable, long batchId, IReadOnlyList<string> waybillNos)
    {
        var normalizedWaybillNos = waybillNos
            .Where(no => !string.IsNullOrWhiteSpace(no))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedWaybillNos.Count == 0) return;

        ValidateTableName(resultTable);
        var costTable = $"{resultTable}_成本明细";
        ValidateTableName(costTable);
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            using (var createCmd = new SqlCommand(
                "CREATE TABLE #TmpRetryWaybillNos ([FWaybillNo] NVARCHAR(50) NOT NULL);",
                connection,
                transaction))
            {
                await createCmd.ExecuteNonQueryAsync();
            }

            var table = new DataTable();
            table.Columns.Add("FWaybillNo", typeof(string));
            foreach (var waybillNo in normalizedWaybillNos)
                table.Rows.Add(waybillNo);

            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "#TmpRetryWaybillNos";
                bulkCopy.BatchSize = 5000;
                await bulkCopy.WriteToServerAsync(table);
            }

            // 限定本批次：避免删掉其它批次同运单号的历史计费结果（运单号跨批次可重复）。
            var deleteSql = $@"
                IF OBJECT_ID(N'{costTable}', N'U') IS NOT NULL
                BEGIN
                    DELETE c FROM [{costTable}] c
                    INNER JOIN [{resultTable}] r ON c.[F计费结果ID] = r.[FID]
                    INNER JOIN #TmpRetryWaybillNos t ON r.[F运单编号] = t.[FWaybillNo]
                    WHERE r.[F批次ID] = @batchId;
                END;

                DELETE r FROM [{resultTable}] r
                INNER JOIN #TmpRetryWaybillNos t ON r.[F运单编号] = t.[FWaybillNo]
                WHERE r.[F批次ID] = @batchId;

                DROP TABLE #TmpRetryWaybillNos;";
            using (var deleteCmd = new SqlCommand(deleteSql, connection, transaction))
            {
                deleteCmd.CommandTimeout = 120;
                deleteCmd.Parameters.AddWithValue("@batchId", batchId);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>删除同批次同类型的旧错误明细（重跑时避免重复堆积）</summary>
    private async Task DeleteExistingErrorsAsync(long batchId, string errorType)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand(
            "DELETE FROM [CF批次错误] WHERE [F批次ID] = @batchId AND [F错误类型] = @errorType",
            connection);
        cmd.Parameters.AddWithValue("@batchId", batchId);
        cmd.Parameters.AddWithValue("@errorType", errorType);
        await cmd.ExecuteNonQueryAsync();
    }

    // ═══════════════════ IQualityIssueTypeProvider ═══════════════════

    public IEnumerable<QualityIssueTypeDefinition> GetIssueTypeDefinitions()
    {
        yield return new QualityIssueTypeDefinition(
            Code: "ERR_SHOP_EMPTY",
            Name: "店铺未匹配",
            Module: "Express",
            Category: "Pricing",
            SeverityLevel: "Error",
            DetailRoute: "/cardflow/issues",
            SourceAutoPlugin: "PricingPlugin",
            Description: "运单的寄件人信息无法匹配到任何已登记的店铺",
            SuggestedFix: "请在CRM模块中补充该客户的店铺信息"
        );
        yield return new QualityIssueTypeDefinition(
            Code: "ERR_SHOP_PENDING_CONFIG",
            Name: "店铺待配置报价",
            Module: "Express",
            Category: "Pricing",
            SeverityLevel: "Error",
            DetailRoute: "/cardflow/issues",
            SourceAutoPlugin: "PricingPlugin",
            Description: "店铺已匹配但尚未配置报价方案",
            SuggestedFix: "请为该店铺配置报价方案"
        );
        yield return new QualityIssueTypeDefinition(
            Code: "ERR_NO_WEIGHT",
            Name: "重量缺失",
            Module: "Express",
            Category: "Pricing",
            SeverityLevel: "Error",
            DetailRoute: "/cardflow/issues",
            SourceAutoPlugin: "PricingPlugin",
            Description: "运单重量为空或为零，无法执行价格计算",
            SuggestedFix: "请检查源数据中的重量字段"
        );
        yield return new QualityIssueTypeDefinition(
            Code: "ERR_NO_PRICE_CELL",
            Name: "无匹配价格区间",
            Module: "Express",
            Category: "Pricing",
            SeverityLevel: "Error",
            DetailRoute: "/cardflow/issues",
            SourceAutoPlugin: "PricingPlugin",
            Description: "根据目的地和重量段未找到匹配的价格单元格",
            SuggestedFix: "请检查报价方案中的省份/重量段覆盖是否完整"
        );
        yield return new QualityIssueTypeDefinition(
            Code: "ERR_NO_PRICE_PLAN",
            Name: "无报价方案",
            Module: "Express",
            Category: "Pricing",
            SeverityLevel: "Error",
            DetailRoute: "/cardflow/issues",
            SourceAutoPlugin: "PricingPlugin",
            Description: "店铺关联的报价方案不存在或已过期",
            SuggestedFix: "请为店铺绑定有效的报价方案"
        );
        yield return new QualityIssueTypeDefinition(
            Code: "ERR_NETWORK_POINT_NOT_FOUND",
            Name: "网点未识别",
            Module: "Express",
            Category: "Network",
            SeverityLevel: "Error",
            DetailRoute: "/cardflow/issues",
            SourceAutoPlugin: "PricingPlugin",
            Description: "运单所属网点名称无法匹配到系统中已登记的网点",
            SuggestedFix: "请在网点管理中添加该网点或设置别名"
        );
        yield return new QualityIssueTypeDefinition(
            Code: "ERR_NETWORK_POINT_MISMATCH",
            Name: "网点不一致",
            Module: "Express",
            Category: "Network",
            SeverityLevel: "Warning",
            DetailRoute: "/express/quality-center/dashboard",
            SourceAutoPlugin: "PricingPlugin",
            Description: "运单映射网点与报价方案网点不一致",
            SuggestedFix: "请确认是否调整网点映射、报价方案或忽略该提醒"
        );
        yield return new QualityIssueTypeDefinition(
            Code: "ERR_UNKNOWN",
            Name: "价格计算未知异常",
            Module: "Express",
            Category: "Pricing",
            SeverityLevel: "Error",
            DetailRoute: "/cardflow/issues",
            SourceAutoPlugin: "PricingPlugin",
            Description: "价格计算过程中发生未分类异常",
            SuggestedFix: "请检查价格方案配置、源数据和系统日志"
        );
    }
}
