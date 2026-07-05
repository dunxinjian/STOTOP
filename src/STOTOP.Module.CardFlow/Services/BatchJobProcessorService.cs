using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 后台批次任务处理器：
/// 1. 启动时崩溃恢复：扫描卡住的 CfBatch（FStatus IN (0,2,4)，以及"卡在 3 但批次级链未完成"的批次）AND FIsRevoked=0 重新入队
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
                // 后台 Channel 消费者无 HttpContext：显式设租户上下文，经 AsyncLocal 穿透整条批次处理链
                // （BatchTriggerService→FlowEngineService→插件/事件），令 ITenantScoped 读写不被 fail-closed 硬墙挡。
                // v2 多租户：按【批次组织】解析租户(而非一律根租户)，覆盖所有批次种类(ParseAndStage/FanOut/ProcessBatchStages)，
                // 避免某租户批次在根租户上下文处理导致漏/串。批次不存在(已删)时兜底根租户。
                var orgContext = scope.ServiceProvider.GetService<IOrgContextAccessor>();
                if (orgContext != null)
                {
                    var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
                    var resolver = scope.ServiceProvider.GetService<ITenantResolver>();
                    var batchOrgId = await db.Set<CfBatch>().IgnoreQueryFilters()
                        .Where(b => b.FID == job.BatchId)
                        .Select(b => (long?)b.FOrgId)
                        .FirstOrDefaultAsync(stoppingToken);
                    orgContext.CurrentTenantId = batchOrgId.HasValue
                        ? resolver?.ResolveTenantForOrg(batchOrgId.Value)
                        : resolver?.GetRootTenantId();
                }
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

        var pending = await SelectStuckBatchesAsync(db, cutoff, ct);

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
            var kind = MapRecoveryKind(b.FStatus, batchAutoSet.Contains(b.FFlowDefinitionId));
            await _channel.Writer.WriteAsync(new BatchJob(b.FID, kind), ct);
            _logger.LogWarning("BatchJobProcessor 崩溃恢复：批次 {BatchId} 状态 {Status} 已超时（FUpdatedTime<{Cutoff}），重新入队 ({Kind})",
                b.FID, b.FStatus, cutoff, kind);
        }
    }

    /// <summary>
    /// 选出需要崩溃恢复的卡住批次（陈旧且未撤销）。
    /// 基础盲区：解析中(0)/质检中(2)/处理中(4)。
    /// 扩展盲区：已创建卡片(3) 但"批次级自动插件链实际未完成"的批次——
    ///   质量分析完成后 GetPostPluginBatchStatus 会把批次置为 3，但自动凭证/汇总等后续批次级节点尚未跑；
    ///   若此刻进程被取消/重启（stoppingToken 触发 ct.ThrowIfCancellationRequested），后续节点从未执行，
    ///   而仅靠 {0,2,4} 无法捞回 → 批次被永久遗弃、凭证永不生成（见 batch #3262）。
    ///   守卫：存在该批次的批次级 CfPluginExecution 处于非终态(10 待处理 / 11 进行中)；
    ///        已 fan-out 到卡片级的正常 3 态批次其批次级执行记录全为 12，不会被误选。
    /// 恢复期无 HttpContext/租户上下文，两处查询均 IgnoreQueryFilters 绕过组织/租户过滤器。
    /// </summary>
    internal static async Task<List<StuckBatch>> SelectStuckBatchesAsync(
        STOTOPDbContext db, DateTime cutoff, CancellationToken ct)
    {
        var candidates = await db.Set<CfBatch>()
            .IgnoreQueryFilters()
            .Where(b => !b.FIsRevoked
                && (b.FUpdatedTime == null || b.FUpdatedTime < cutoff)
                && (b.FStatus == 0 || b.FStatus == 2 || b.FStatus == 3 || b.FStatus == 4))
            .Select(b => new StuckBatch(b.FID, b.FStatus, b.FFlowDefinitionId))
            .ToListAsync(ct);
        if (candidates.Count == 0) return candidates;

        // FStatus=3 需额外守卫：仅捞"批次级链未完成"（存在非终态执行记录）的批次。
        var status3Ids = candidates.Where(c => c.FStatus == 3).Select(c => c.FID).ToList();
        if (status3Ids.Count == 0) return candidates;

        var unfinished3 = (await db.Set<CfPluginExecution>()
            .IgnoreQueryFilters()
            .Where(e => status3Ids.Contains(e.FBatchId) && (e.FStatus == 10 || e.FStatus == 11))
            .Select(e => e.FBatchId)
            .Distinct()
            .ToListAsync(ct))
            .ToHashSet();

        return candidates.Where(c => c.FStatus != 3 || unfinished3.Contains(c.FID)).ToList();
    }

    /// <summary>状态 → 恢复入队 Kind 映射（纯函数，便于单测）。</summary>
    internal static BatchJobKind MapRecoveryKind(int status, bool isBatchAutoFlow) => status switch
    {
        // 解析中：按流程是否含批次级自动节点判定走新(批次级链)/旧(解析入库)路径
        0 => isBatchAutoFlow ? BatchJobKind.ProcessBatchStages : BatchJobKind.ParseAndStage,
        // 处理中(4) 与 卡在质量后未完成(3)：统一从断点续跑批次级节点链（ProcessBatchStagesAsync 依 FCurrentBatchStageOrder 续跑，不重跑导入）
        3 => BatchJobKind.ProcessBatchStages,
        4 => BatchJobKind.ProcessBatchStages,
        // 质检中(2)：走质检+fan-out
        _ => BatchJobKind.QualityCheckAndFanOut,
    };

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

/// <summary>崩溃恢复选出的卡住批次（供 SelectStuckBatchesAsync 返回、单测断言）。</summary>
internal readonly record struct StuckBatch(long FID, int FStatus, long FFlowDefinitionId);
