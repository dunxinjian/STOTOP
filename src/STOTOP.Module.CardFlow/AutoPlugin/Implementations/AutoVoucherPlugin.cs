using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Handlers;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>自动凭证插件，委托给 AutoVoucherHandler 执行凭证生成逻辑，集成 WF 链路追踪</summary>
public class AutoVoucherPlugin : BatchPluginBase
{
    private readonly AutoVoucherHandler _handler;
    private readonly IPluginProgressReporter _progressReporter;
    private readonly ILogger<AutoVoucherPlugin> _logger;
    private readonly STOTOPDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly SystemServiceAccountOptions _serviceAccountOptions;

    public override string PluginName => "AutoVoucher";
    public override string DisplayName => "自动凭证";

    public AutoVoucherPlugin(
        AutoVoucherHandler handler,
        IPluginProgressReporter progressReporter,
        ILogger<AutoVoucherPlugin> logger,
        STOTOPDbContext dbContext,
        IServiceProvider serviceProvider,
        IOptions<SystemServiceAccountOptions> serviceAccountOptions)
    {
        _handler = handler;
        _progressReporter = progressReporter;
        _logger = logger;
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
        _serviceAccountOptions = serviceAccountOptions.Value;
    }

    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        const int totalSteps = 3;

        // Step 1: 加载凭证规则
        await _progressReporter.ReportProgressAsync(context.BatchId, 0, totalSteps, "加载凭证规则");

        // 确定目标表
        var targetTable = GetTargetTable(context);

        // 加载配置
        var config = await LoadPluginConfigAsync(context);
        var root = config?.RootElement;

        // 解析配置
        string? handlerConfig = null;
        long? periodId = null;
        long? accountSetId = await GetAccountSetIdAsync(context);
        long orgId = await GetOrgIdAsync(context);
        long creatorId = 0;

        if (root != null)
        {
            handlerConfig = BuildHandlerConfigJson(root.Value, context.BatchId, orgId);
            if (!accountSetId.HasValue && root.Value.TryGetProperty("accountSetId", out var asid) && asid.ValueKind == JsonValueKind.Number)
                accountSetId = asid.GetInt64();
            if (root.Value.TryGetProperty("periodId", out var pid) && pid.ValueKind == JsonValueKind.Number)
                periodId = pid.GetInt64();
            if (root.Value.TryGetProperty("creatorId", out var cid) && cid.ValueKind == JsonValueKind.Number)
                creatorId = cid.GetInt64();
        }

        if (!accountSetId.HasValue && orgId == 0)
        {
            return PluginResult.Fail("无法确定账套：组织上下文和配置均未提供 accountSetId");
        }

        // 如果没有账套ID，查询默认账套
        if (!accountSetId.HasValue)
        {
            var defaultAccountSet = await _dbContext.Set<FinAccountSet>()
                .AsNoTracking()
                .Where(a => a.FIsDefault && a.FStatus == 1 && (orgId == 0 || a.FOrgId == orgId))
                .FirstOrDefaultAsync();
            accountSetId = defaultAccountSet?.FID;
        }

        // 自动推导 PeriodId
        if (!periodId.HasValue && accountSetId.HasValue)
        {
            periodId = await DerivePeriodIdAsync(root, targetTable, context.BatchId, accountSetId.Value);
        }

        await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "加载凭证规则");

        // ═══ 防御：ruleGroups 为空 + unmatchedAction=skip → 跳过凭证生成 ═══
        if (root != null)
        {
            var ruleGroups = root.Value.TryGetProperty("ruleGroups", out var rgProp)
                && rgProp.ValueKind == JsonValueKind.Array ? rgProp : (JsonElement?)null;
            var unmatchedAction = root.Value.TryGetProperty("unmatchedAction", out var uaProp)
                && uaProp.ValueKind == JsonValueKind.String ? uaProp.GetString() : null;

            if ((ruleGroups == null || ruleGroups.Value.GetArrayLength() == 0)
                && string.Equals(unmatchedAction, "skip", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "AutoVoucherPlugin: 批次={BatchId} ruleGroups为空且unmatchedAction=skip，跳过凭证生成",
                    context.BatchId);
                await _progressReporter.ReportProgressAsync(context.BatchId, 3, totalSteps, "跳过凭证生成");
                return PluginResult.Ok("凭证规则未配置（ruleGroups为空），已跳过", 0);
            }
        }

        // Step 2: 查询暂存数据
        await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "查询暂存数据");

        var classification = new ClassificationItem
        {
            Type = "AutoVoucher",
            Severity = "Info",
            AffectedRowIds = new List<long>(),
            AffectedRowCount = 0
        };

        var handlerContext = new HandlerContext
        {
            BatchId = context.BatchId,
            TargetTable = targetTable,
            Classification = classification,
            HandlerConfig = handlerConfig,
            OrgId = orgId,
            CreatorId = creatorId,
            PeriodId = periodId,
            AccountSetId = accountSetId
        };

        await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "查询暂存数据");

        // Step 3: 生成凭证
        await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "生成凭证");

        _logger.LogInformation("AutoVoucherPlugin: 执行前 - BatchId={BatchId}, PeriodId={PeriodId}, AccountSetId={AccountSetId}, TargetTable={TargetTable}",
            context.BatchId, periodId, accountSetId, targetTable);

        try
        {
            var handlerResult = await _handler.HandleAsync(handlerContext);

            _logger.LogInformation("AutoVoucherPlugin: 执行结果 - Success={Success}, Message={Message}",
                handlerResult.Success, handlerResult.Message);

            if (handlerResult.Success)
            {
                await _progressReporter.ReportProgressAsync(context.BatchId, 3, totalSteps, "生成凭证");
                return PluginResult.Ok(handlerResult.Message, handlerResult.Output.TryGetValue("GeneratedVoucherCount", out var vc) ? Convert.ToInt32(vc) : 0);
            }
            else
            {
                return PluginResult.Fail(handlerResult.Message ?? "自动凭证生成失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AutoVoucherPlugin: 执行异常，批次={BatchId}", context.BatchId);
            return PluginResult.Fail($"自动凭证异常: {ex.Message}");
        }
    }

    public override Task RollbackAsync(PluginContext context)
    {
        _logger.LogWarning("AutoVoucherPlugin: 回撤请求，批次={BatchId}。凭证删除需通过VoucherService手动处理。", context.BatchId);
        return Task.CompletedTask;
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "根据凭证规则自动生成财务凭证，支持简单聚合和规则引擎两种模式";
        return metadata;
    }

    private string GetTargetTable(PluginContext context)
    {
        var batch = _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .FirstOrDefault(b => b.FID == context.BatchId);
        var batchTable = batch?.FActualTargetTable;

        if (string.IsNullOrWhiteSpace(batchTable))
            throw new InvalidOperationException("批次记录中未找到暂存表信息（FActualTargetTable为空）");

        return batchTable;
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

    private async Task<long?> GetAccountSetIdAsync(PluginContext context)
    {
        var batch = await _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .Where(b => b.FID == context.BatchId)
            .Select(b => b.FAccountSetId)
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

    /// <summary>自动推导会计期间</summary>
    private async Task<long?> DerivePeriodIdAsync(JsonElement? root, string targetTable, long batchId, long accountSetId)
    {
        try
        {
            string dateField = "F业务日期";
            if (root.HasValue)
            {
                var r = root.Value;
                if (r.TryGetProperty("dateField", out var dfProp) && dfProp.ValueKind == JsonValueKind.String)
                    dateField = dfProp.GetString() ?? "F业务日期";
            }

            if (!Regex.IsMatch(dateField, @"^F[\u4e00-\u9fa5A-Za-z0-9_]+$"))
            {
                _logger.LogWarning("AutoVoucherPlugin: dateField 不合法，跳过自动推导 - DateField={DateField}", dateField);
                return null;
            }

            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            var dateSql = $"SELECT TOP 1 [{dateField}] FROM [{targetTable}] WHERE [F批次ID] = @batchId AND [{dateField}] IS NOT NULL";
            using var cmd = connection.CreateCommand();
            cmd.CommandText = dateSql;
            var param = cmd.CreateParameter();
            param.ParameterName = "@batchId";
            param.Value = batchId;
            cmd.Parameters.Add(param);

            var dateResult = await cmd.ExecuteScalarAsync();
            DateTime? businessDate = null;
            if (dateResult != null && dateResult != DBNull.Value)
            {
                if (dateResult is DateTime dt)
                    businessDate = dt;
                else if (DateTime.TryParse(dateResult.ToString(), out var parsed))
                    businessDate = parsed;
            }

            if (businessDate.HasValue)
            {
                var period = await _dbContext.Set<FinAccountPeriod>()
                    .Where(p => p.FStartDate <= businessDate.Value
                             && p.FEndDate >= businessDate.Value
                             && p.FAccountSetId == accountSetId
                             && p.FStatus == 1)
                    .FirstOrDefaultAsync();

                if (period != null)
                {
                    _logger.LogInformation("AutoVoucherPlugin: 自动推导会计期间 - BusinessDate={Date}, PeriodId={PeriodId}",
                        businessDate.Value, period.FID);
                    return period.FID;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AutoVoucherPlugin: 自动推导PeriodId失败，BatchId={BatchId}", batchId);
        }
        return null;
    }

    /// <summary>将规则配置 JSON 封装为 AutoVoucherHandler 期望的输入格式（V2 统一透传）</summary>
    private static string BuildHandlerConfigJson(JsonElement root, long batchId, long orgId)
    {
        var v2Result = new Dictionary<string, object?>
        {
            ["version"] = 2,
            ["runtimeContext"] = new { batchId, orgId },
            ["ruleConfig"] = JsonSerializer.Deserialize<JsonElement>(root.GetRawText())
        };
        return JsonSerializer.Serialize(v2Result);
    }

}
