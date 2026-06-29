using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 后台批次任务处理器：
/// 1. 启动时崩溃恢复：扫描 CfBatch FStatus IN (0, 2) AND FIsRevoked=0 重新入队
/// 2. 持续从 Channel 读取 BatchJob 并调用 BatchTriggerService.ProcessBatchJobAsync
/// </summary>
public class BatchJobProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Channel<BatchJob> _channel;
    private readonly ILogger<BatchJobProcessorService> _logger;

    public BatchJobProcessorService(
        IServiceScopeFactory scopeFactory,
        Channel<BatchJob> channel,
        ILogger<BatchJobProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        // 崩溃恢复
        try
        {
            await RecoverPendingBatchesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BatchJobProcessor 崩溃恢复失败");
        }

        await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var trigger = scope.ServiceProvider.GetRequiredService<IBatchTriggerService>();
                await trigger.ProcessBatchJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BatchJobProcessor 处理任务失败 BatchId={BatchId} Kind={Kind}", job.BatchId, job.Kind);
            }
        }
    }

    private async Task RecoverPendingBatchesAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
    
        // 表存在性保护：首次启动时表可能尚未创建
        if (!await TableExistsAsync(db, "CF批次", ct))
        {
            _logger.LogInformation("CF批次表尚未创建，跳过崩溃恢复");
            return;
        }
    
        // 超时阈值：FUpdatedTime 超过10分钟未更新视为卡住（新增状态4=处理中）
        var cutoff = DateTime.Now.AddMinutes(-10);
    
        var pending = await db.Set<CfBatch>()
            .IgnoreQueryFilters()
            .Where(b => !b.FIsRevoked
                && (b.FStatus == 0 || b.FStatus == 2 || b.FStatus == 4)
                && (b.FUpdatedTime == null || b.FUpdatedTime < cutoff))
            .Select(b => new { b.FID, b.FStatus, b.FFlowDefinitionId })
            .ToListAsync(ct);
    
        if (pending.Count == 0)
        {
            _logger.LogInformation("BatchJobProcessor 崩溃恢复：无卡住的待处理批次");
            return;
        }
    
        // 预加载"含批次级自动节点"的流程定义 ID，用于判定 FStatus=0 应走新流程还是旧流程
        // 兼容条件：旧版 FType="batchAuto" 或 新版 FType="auto" + F处理粒度="batch"
        var flowIds = pending.Select(b => b.FFlowDefinitionId).Distinct().ToList();
        var batchAutoFlowIds = await db.Set<CfStageDefinition>()
            .Join(db.Set<CfFlowVersion>(),
                s => s.FFlowVersionId,
                v => v.FID,
                (s, v) => new { s, v })
            .Where(x => x.v.FIsCurrentVersion
                        && (x.s.FType == "batchAuto"
                            || (x.s.FType == "auto" && x.s.F处理粒度 == "batch"))
                        && flowIds.Contains(x.v.FFlowDefinitionId))
            .Select(x => x.v.FFlowDefinitionId)
            .Distinct()
            .ToListAsync(ct);
        var batchAutoSet = new HashSet<long>(batchAutoFlowIds);
    
        foreach (var b in pending)
        {
            BatchJobKind kind;
            if (b.FStatus == 0)
            {
                kind = batchAutoSet.Contains(b.FFlowDefinitionId)
                    ? BatchJobKind.ProcessBatchStages
                    : BatchJobKind.ParseAndStage;
            }
            else if (b.FStatus == 4)
            {
                // 状态4=处理中且超时，统一走批次级节点链重新处理
                kind = BatchJobKind.ProcessBatchStages;
            }
            else
            {
                kind = BatchJobKind.QualityCheckAndFanOut;
            }
            await _channel.Writer.WriteAsync(new BatchJob(b.FID, kind), ct);
            _logger.LogWarning("BatchJobProcessor 崩溃恢复：批次 {BatchId} 状态 {Status} 已超时（FUpdatedTime<{Cutoff}），重新入队 ({Kind})",
                b.FID, b.FStatus, cutoff, kind);
        }
    }

    private static async Task<bool> TableExistsAsync(STOTOPDbContext db, string tableName, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == global::System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}' AND TABLE_SCHEMA = 'dbo') THEN 1 ELSE 0 END";
            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result) == 1;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
    }
}
