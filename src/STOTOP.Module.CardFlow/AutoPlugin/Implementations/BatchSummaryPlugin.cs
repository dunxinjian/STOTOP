using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>批次汇总插件：在批次级链末尾生成一张汇总卡片</summary>
public class BatchSummaryPlugin : BatchPluginBase
{
    public override string PluginName => "BatchSummary";
    public override string DisplayName => "批次汇总";

    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<BatchSummaryPlugin> _logger;

    public BatchSummaryPlugin(STOTOPDbContext dbContext, ILogger<BatchSummaryPlugin> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        // 1. 读取批次信息
        var batch = await _dbContext.Set<CfBatch>()
            .FirstAsync(b => b.FID == context.BatchId, context.CancellationToken);

        // 2. 统计已生成凭证数（用 Sum 而非 Count，强制转 nullable 防止空结果集异常）
        var voucherCount = await _dbContext.Set<CfVoucherRecord>()
            .Where(v => v.FBatchId == context.BatchId)
            .SumAsync(v => (int?)v.FGeneratedVoucherCount, context.CancellationToken) ?? 0;

        // 3. 获取当前发布版本
        var currentVersion = await _dbContext.Set<CfFlowVersion>()
            .Where(v => v.FFlowDefinitionId == batch.FFlowDefinitionId && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .FirstOrDefaultAsync(context.CancellationToken);

        // 4. 构建汇总 JSON
        var summary = new
        {
            totalRows = batch.FTotalRows,
            successRows = batch.FSuccessRows,
            failedRows = batch.FFailedRows,
            voucherCount,
            targetTable = batch.FActualTargetTable,
            importTime = batch.FCreatedTime,
            batchId = batch.FID
        };

        // 5. 创建 ONE CfCard（status=draft，后续由 FlowEngine 自动激活并分配处理人）
        var card = new CfCard
        {
            FFlowDefinitionId = batch.FFlowDefinitionId,
            FFlowVersionId = currentVersion?.FID ?? 0,
            FOrgId = batch.FOrgId,
            FBatchId = batch.FID,
            FTitle = $"导入汇总 - {batch.FTotalRows}行/{voucherCount}张凭证",
            FDataJson = JsonSerializer.Serialize(summary),
            FStatus = "draft",
            FInitiatorId = batch.FTriggeredById,
            FInitiatorName = "系统",
            FCurrentRound = 0,
            FCreatedTime = DateTime.Now
        };
        _dbContext.Set<CfCard>().Add(card);
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "BatchSummaryPlugin: 批次={BatchId} 汇总卡片已创建, 导入行数={Total}, 凭证数={Vouchers}",
            context.BatchId, batch.FTotalRows, voucherCount);

        return PluginResult.Ok($"汇总卡片已创建: {batch.FTotalRows}行, {voucherCount}张凭证", 1);
    }
}
