using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Handlers;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>告警通知插件，委托给 AlertNotifyHandler 执行告警发送逻辑</summary>
public class AlertNotifyPlugin : ProcessingPluginBase
{
    private readonly AlertNotifyHandler _handler;
    private readonly IPluginProgressReporter _progressReporter;
    private readonly ILogger<AlertNotifyPlugin> _logger;
    private readonly STOTOPDbContext _dbContext;

    public override string PluginName => "AlertNotify";
    public override string DisplayName => "告警通知";

    public AlertNotifyPlugin(
        AlertNotifyHandler handler,
        IPluginProgressReporter progressReporter,
        ILogger<AlertNotifyPlugin> logger,
        STOTOPDbContext dbContext)
    {
        _handler = handler;
        _progressReporter = progressReporter;
        _logger = logger;
        _dbContext = dbContext;
    }

    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        const int totalSteps = 2;

        // Step 1: 评估告警条件
        await _progressReporter.ReportProgressAsync(context.BatchId, 0, totalSteps, "评估告警条件");

        var targetTable = GetTargetTable(context);
        var classification = await LoadClassificationAsync(context)
            ?? new ClassificationItem
            {
                Type = "AlertNotify",
                Severity = "Warning",
                AffectedRowIds = new List<long>(),
                AffectedRowCount = 0
            };

        // 从 CfPluginRule 读取配置
        string? handlerConfig = null;
        var config = await LoadPluginConfigAsync(context);
        if (config != null)
        {
            var root = config.RootElement;
            if (root.TryGetProperty("handlerConfig", out var hc))
                handlerConfig = hc.GetRawText();
        }

        var handlerContext = new HandlerContext
        {
            BatchId = context.BatchId,
            TargetTable = targetTable,
            Classification = classification,
            HandlerConfig = handlerConfig
        };

        await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "评估告警条件");

        // Step 2: 发送通知
        await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "发送通知");

        _logger.LogInformation("AlertNotifyPlugin: 开始执行，批次={BatchId}，表={Table}", context.BatchId, targetTable);

        try
        {
            var handlerResult = await _handler.HandleAsync(handlerContext);

            if (handlerResult.Success)
            {
                await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "发送通知");
                return PluginResult.Ok(handlerResult.Message);
            }
            else
            {
                // 告警失败不应阻断管道
                return PluginResult.Fail(handlerResult.Message ?? "告警通知发送失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AlertNotifyPlugin: 执行异常，批次={BatchId}", context.BatchId);
            return PluginResult.Fail($"告警通知异常: {ex.Message}");
        }
    }

    public override Task RollbackAsync(PluginContext context)
    {
        // 通知无法撤回，空实现
        _logger.LogDebug("AlertNotifyPlugin: 通知无法撤回，跳过回撤，批次={BatchId}", context.BatchId);
        return Task.CompletedTask;
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "根据数据触发告警通知（钉钉、邮件等）";
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

    private async Task<ClassificationItem?> LoadClassificationAsync(PluginContext context)
    {
        var dispatchResult = await _dbContext.Set<CfSystemDispatchResult>()
            .AsNoTracking()
            .Where(d => d.FBatchId == context.BatchId)
            .OrderByDescending(d => d.FID)
            .FirstOrDefaultAsync();

        if (dispatchResult == null) return null;

        return new ClassificationItem
        {
            Type = dispatchResult.FRuleName ?? "AlertNotify",
            Severity = "Warning",
            AffectedRowIds = new List<long>(),
            AffectedRowCount = 0
        };
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
}
