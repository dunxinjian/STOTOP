using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.AutoPlugin.Implementations;

/// <summary>
/// 卡片展开插件（Fan-Out）：将批次明细行展开为独立卡片
/// 必须是批次级节点链的最后一个节点
/// </summary>
public class FanOutPlugin : BatchPluginBase
{
    public override string PluginName => "FanOut";
    public override string DisplayName => "卡片展开";

    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<FanOutPlugin> _logger;

    public FanOutPlugin(STOTOPDbContext dbContext, ILogger<FanOutPlugin> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override async Task<PluginResult> ExecuteAsync(PluginContext context)
    {
        // 1. 读取当前批次所有待质检的行（FStatus=0）
        var rows = await _dbContext.Set<CfBatchRow>()
            .Where(r => r.FBatchId == context.BatchId && r.FStatus == 0)
            .OrderBy(r => r.FRowNo)
            .ToListAsync(context.CancellationToken);

        if (rows.Count == 0)
        {
            _logger.LogWarning("FanOutPlugin: 批次 {BatchId} 无待处理行", context.BatchId);
            return PluginResult.Ok("无待处理行", 0);
        }

        // 2. 获取批次信息以确定流程定义ID
        var batch = await _dbContext.Set<CfBatch>()
            .AsNoTracking()
            .FirstAsync(b => b.FID == context.BatchId, context.CancellationToken);

        // 3. 获取当前发布版本（卡片需要锁定 FFlowVersionId）
        var currentVersion = await _dbContext.Set<CfFlowVersion>()
            .Where(v => v.FFlowDefinitionId == batch.FFlowDefinitionId && v.FIsCurrentVersion)
            .OrderByDescending(v => v.FVersionNumber)
            .FirstOrDefaultAsync(context.CancellationToken);

        // 4. 逐行质检 + 创建卡片
        int successCount = 0, failCount = 0;
        var now = DateTime.Now;
        var cardRowMap = new List<(CfBatchRow row, CfCard card)>();

        foreach (var row in rows)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // 简单质检：FDataJson 不能为空
            if (string.IsNullOrWhiteSpace(row.FDataJson))
            {
                row.FStatus = 2; // 质检失败
                row.FErrorMessage = "数据为空";
                row.FUpdatedTime = now;
                failCount++;
                continue;
            }

            // 质检通过 → 创建卡片
            var card = new CfCard
            {
                FFlowDefinitionId = batch.FFlowDefinitionId,
                FFlowVersionId = currentVersion?.FID ?? 0,
                FOrgId = batch.FOrgId,
                FBatchId = batch.FID,
                FDataJson = row.FDataJson,
                FStatus = "draft",
                FInitiatorId = batch.FTriggeredById,
                FInitiatorName = "系统",
                FCurrentRound = 0,
                FCreatedTime = now,
            };
            _dbContext.Set<CfCard>().Add(card);

            row.FStatus = 3; // 已创建卡片
            row.FUpdatedTime = now;
            cardRowMap.Add((row, card));
            successCount++;
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);

        // 5. 回填 CfBatchRow.FCardId（EF Core 在 SaveChanges 后自动填充 FID）
        foreach (var (row, card) in cardRowMap)
        {
            row.FCardId = card.FID;
        }

        // 6. 更新批次统计
        var batchEntity = await _dbContext.Set<CfBatch>()
            .FirstAsync(b => b.FID == context.BatchId, context.CancellationToken);
        batchEntity.FSuccessRows = successCount;
        batchEntity.FFailedRows = failCount;
        batchEntity.FTotalRows = rows.Count;
        batchEntity.FStatus = 3; // 已创建卡片
        batchEntity.FUpdatedTime = now;
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "FanOutPlugin 完成: 批次={BatchId}, 总行={Total}, 成功创建卡片={Success}, 质检失败={Fail}",
            context.BatchId, rows.Count, successCount, failCount);

        return PluginResult.Ok($"卡片展开完成: {successCount}张卡片已创建", successCount);
    }
}
