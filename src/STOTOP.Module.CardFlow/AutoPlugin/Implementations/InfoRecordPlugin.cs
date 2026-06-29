using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Handlers;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>信息记录插件，委托给 InfoRecordHandler 执行信息记录逻辑</summary>
public class InfoRecordPlugin : ProcessingPluginBase
{
    private readonly InfoRecordHandler _handler;
    private readonly IPluginProgressReporter _progressReporter;
    private readonly ILogger<InfoRecordPlugin> _logger;
    private readonly STOTOPDbContext _dbContext;

    public override string PluginName => "InfoRecord";
    public override string DisplayName => "信息记录";

    public InfoRecordPlugin(
        InfoRecordHandler handler,
        IPluginProgressReporter progressReporter,
        ILogger<InfoRecordPlugin> logger,
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

        // Step 1: 采集信息
        await _progressReporter.ReportProgressAsync(context.BatchId, 0, totalSteps, "采集信息");

        var targetTable = GetTargetTable(context);
        var classification = await LoadClassificationAsync(context)
            ?? new ClassificationItem
            {
                Type = "InfoRecord",
                Severity = "Info",
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

        await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "采集信息");

        // Step 2: 写入记录
        await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "写入记录");

        _logger.LogInformation("InfoRecordPlugin: 开始执行，批次={BatchId}，表={Table}", context.BatchId, targetTable);

        try
        {
            var handlerResult = await _handler.HandleAsync(handlerContext);

            if (handlerResult.Success)
            {
                await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "写入记录");
                return PluginResult.Ok(handlerResult.Message);
            }
            else
            {
                return PluginResult.Fail(handlerResult.Message ?? "信息记录失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InfoRecordPlugin: 执行异常，批次={BatchId}", context.BatchId);
            return PluginResult.Fail($"信息记录异常: {ex.Message}");
        }
    }

    public override Task RollbackAsync(PluginContext context)
    {
        // 信息记录的回撤：日志记录无法删除，此处仅记录回撤请求
        _logger.LogInformation("InfoRecordPlugin: 回撤请求，信息记录无法删除，批次={BatchId}", context.BatchId);
        return Task.CompletedTask;
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "将处理结果记录到信息日志";
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
        // 从批次最近的分类派发结果中加载（替代原 SharedData["ClassificationResult"]）
        var dispatchResult = await _dbContext.Set<CfSystemDispatchResult>()
            .AsNoTracking()
            .Where(d => d.FBatchId == context.BatchId)
            .OrderByDescending(d => d.FID)
            .FirstOrDefaultAsync();

        if (dispatchResult == null) return null;

        return new ClassificationItem
        {
            Type = dispatchResult.FRuleName ?? "InfoRecord",
            Severity = "Info",
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
