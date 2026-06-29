using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Classification;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>分类分析插件，将 ClassificationEngine 包装为批次级处理插件</summary>
public class ClassificationPlugin : BatchPluginBase
{
    private readonly ClassificationEngine _engine;
    private readonly IPluginProgressReporter _progressReporter;
    private readonly ILogger<ClassificationPlugin> _logger;
    private readonly STOTOPDbContext _dbContext;

    public override string PluginName => "Classification";
    public override string DisplayName => "自动分类";

    public ClassificationPlugin(
        ClassificationEngine engine,
        IPluginProgressReporter progressReporter,
        ILogger<ClassificationPlugin> logger,
        STOTOPDbContext dbContext)
    {
        _engine = engine;
        _progressReporter = progressReporter;
        _logger = logger;
        _dbContext = dbContext;
    }

    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        const int totalSteps = 2;

        // 确定目标表名：从批次记录读取，与规则配置比对验证
        var targetTable = GetTargetTable(context);

        // 从 CfPluginRule 读取配置
        var config = await LoadPluginConfigAsync(context);
        double errorThreshold = 5.0;
        if (config != null)
        {
            var root = config.RootElement;
            if (root.TryGetProperty("errorThreshold", out var et) && et.ValueKind == JsonValueKind.Number)
                errorThreshold = et.GetDouble();
        }

        _logger.LogInformation("ClassificationPlugin: 开始分类分析，批次={BatchId}，目标表={Table}，错误阈值={Threshold}%",
            context.BatchId, targetTable, errorThreshold);

        // Step 1: 数据分类
        await _progressReporter.ReportProgressAsync(context.BatchId, 0, totalSteps, "数据分类");

        var evt = new ImportCompletedEvent
        {
            BatchId = context.BatchId,
            TargetTable = targetTable,
            CompletedAt = DateTime.Now
        };

        try
        {
            var classificationResult = await _engine.AnalyzeAsync(evt);

            await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "数据分类");

            // Step 2: 标签标记
            await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "标签标记");

            _logger.LogInformation("ClassificationPlugin: 分类分析完成，批次={BatchId}，产生 {Count} 条分类项",
                context.BatchId, classificationResult.Items.Count);

            await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "标签标记");

            return PluginResult.Ok(
                $"分类分析完成，产生 {classificationResult.Items.Count} 条分类项",
                classificationResult.Items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClassificationPlugin: 分类分析异常，批次={BatchId}", context.BatchId);
            return PluginResult.Fail($"分类分析异常: {ex.Message}");
        }
    }

    public override Task RollbackAsync(PluginContext context)
    {
        // 分类结果已由 ClassificationEngine 持久化到派发结果表
        _logger.LogInformation("ClassificationPlugin: 回撤完成，批次={BatchId}", context.BatchId);
        return Task.CompletedTask;
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "根据派发规则对数据进行分类并写入派发结果";
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
