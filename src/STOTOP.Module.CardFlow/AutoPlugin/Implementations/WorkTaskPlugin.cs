using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Handlers;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>工作任务插件，委托给 WorkTaskHandler 执行任务创建逻辑</summary>
public class WorkTaskPlugin : ProcessingPluginBase
{
    private readonly WorkTaskHandler _handler;
    private readonly IPluginProgressReporter _progressReporter;
    private readonly ILogger<WorkTaskPlugin> _logger;
    private readonly STOTOPDbContext _dbContext;

    public override string PluginName => "WorkTask";
    public override string DisplayName => "工作任务";

    public WorkTaskPlugin(
        WorkTaskHandler handler,
        IPluginProgressReporter progressReporter,
        ILogger<WorkTaskPlugin> logger,
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

        // Step 1: 创建工作任务
        await _progressReporter.ReportProgressAsync(context.BatchId, 0, totalSteps, "创建工作任务");

        var targetTable = GetTargetTable(context);
        var classification = await LoadClassificationAsync(context)
            ?? new ClassificationItem
            {
                Type = "WorkTask",
                Severity = "Info",
                AffectedRowIds = new List<long>(),
                AffectedRowCount = 0
            };

        // 从 CfPluginRule 读取配置
        string? handlerConfig = null;
        long orgId = await GetOrgIdAsync(context);
        long creatorId = 0;

        var config = await LoadPluginConfigAsync(context);
        if (config != null)
        {
            var root = config.RootElement;
            if (root.TryGetProperty("handlerConfig", out var hc))
                handlerConfig = hc.GetRawText();
            if (root.TryGetProperty("creatorId", out var cid) && cid.ValueKind == JsonValueKind.Number)
                creatorId = cid.GetInt64();
        }

        var handlerContext = new HandlerContext
        {
            BatchId = context.BatchId,
            TargetTable = targetTable,
            Classification = classification,
            HandlerConfig = handlerConfig,
            OrgId = orgId,
            CreatorId = creatorId
        };

        _logger.LogInformation("WorkTaskPlugin: 开始执行，批次={BatchId}，表={Table}", context.BatchId, targetTable);

        try
        {
            var handlerResult = await _handler.HandleAsync(handlerContext);

            if (handlerResult.Success)
            {
                await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "创建工作任务");

                // Step 2: 通知处理人
                await _progressReporter.ReportProgressAsync(context.BatchId, 1, totalSteps, "通知处理人");
                await _progressReporter.ReportProgressAsync(context.BatchId, 2, totalSteps, "通知处理人");

                return PluginResult.Ok(handlerResult.Message);
            }
            else
            {
                return PluginResult.Fail(handlerResult.Message ?? "工作任务创建失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WorkTaskPlugin: 执行异常，批次={BatchId}", context.BatchId);
            return PluginResult.Fail($"工作任务异常: {ex.Message}");
        }
    }

    public override Task RollbackAsync(PluginContext context)
    {
        // 回撤：取消已创建的任务（日志记录，实际取消需扩展）
        _logger.LogWarning("WorkTaskPlugin: 回撤请求，需取消任务，批次={BatchId}", context.BatchId);
        return Task.CompletedTask;
    }

    public override PluginMetadata GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata.Description = "根据数据自动创建工作任务";
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
            Type = dispatchResult.FRuleName ?? "WorkTask",
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
