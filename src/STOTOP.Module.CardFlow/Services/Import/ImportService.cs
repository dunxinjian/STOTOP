using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services.Import;

/// <summary>
/// 导入服务实现（从 DC 迁移至 CardFlow）
/// 提供批次重试等跨模块调用能力
/// </summary>
public class ImportService : IImportService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<ImportService> _logger;

    public ImportService(STOTOPDbContext context, ILogger<ImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RetryBatchAsync(long batchId)
    {
        var batch = await _context.Set<CfBatch>()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId);

        if (batch == null)
            throw new InvalidOperationException($"批次 {batchId} 不存在");

        // 判断是否可重试
        var retryableStatuses = new[]
        {
            CfBatchStatus.Failed,
            CfBatchStatus.PartiallyCompleted,
            CfBatchStatus.Parsing,
            CfBatchStatus.Staged,
        };

        var isStuck = (batch.FStatus == CfBatchStatus.Processing || batch.FStatus == CfBatchStatus.QualityChecking)
                      && batch.FImportStartTime.HasValue
                      && (DateTime.Now - batch.FImportStartTime.Value).TotalMinutes > 10;

        if (!retryableStatuses.Contains(batch.FStatus) && !isStuck)
            throw new InvalidOperationException($"只有失败或卡住的批次才能重试，当前状态: {batch.FStatus}");

        // 重置状态为解析中，等待后续流程重新处理
        batch.FStatus = CfBatchStatus.Parsing;
        batch.FImportStartTime = DateTime.Now;
        batch.FErrorMessage = null;
        batch.FUpdatedTime = DateTime.Now;

        await _context.SaveChangesAsync();

        _logger.LogInformation("批次 {BatchId} 已重置为重试状态", batchId);
    }
}
