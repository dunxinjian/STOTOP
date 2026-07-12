using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Core.Interfaces;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>批次进度通用 DTO（按节点分组统计）</summary>
public class BatchProgressDto
{
    public long BatchId { get; set; }
    public int TotalCards { get; set; }
    /// <summary>key=节点名称, value=该节点待处理卡片数（active 状态）</summary>
    public Dictionary<string, int> StageProgress { get; set; } = new();
    public int Completed { get; set; }
    public int Failed { get; set; }
    /// <summary>批次状态（CfBatch.FStatus）</summary>
    public int BatchStatus { get; set; }
    public bool IsRevoked { get; set; }
}

public interface IBatchLifecycleService
{
    /// <summary>聚合刷新批次状态（卡片状态变更后调用）</summary>
    Task RefreshBatchStatusAsync(long batchId);

    /// <summary>撤销批次：委托 BatchRevokeHandler 无工单权威路径（级联取消 + 批次软删除 + 撤销日志/事件 + 版本推送）</summary>
    Task RevokeBatchAsync(long batchId, long operatorId);

    /// <summary>撤销级联段：取消未完成卡片（含凭证红冲/编排回调）+ 行明细置 5(已撤销)。供权威撤销路径 BatchRevokeHandler 调用</summary>
    Task CascadeCancelBatchArtifactsAsync(long batchId);

    /// <summary>查询批次进度（按节点名称分组）</summary>
    Task<BatchProgressDto> GetBatchProgressAsync(long batchId);

    /// <summary>原子性批次状态转换：SaveChanges + SignalR 推送</summary>
    Task TransitionBatchStatusAsync(CfBatch batch, int newStatus, string? message = null);

    /// <summary>递增批次变更版本号（SEQ_BatchChange 全局单调），返回新版本号。供撤销/恢复权威路径推送用</summary>
    Task<long> BumpChangeVersionAsync(CfBatch batch);
}

/// <summary>
/// CF 批次生命周期服务：状态聚合 / 撤销级联 / 进度查询
/// </summary>
public class BatchLifecycleService : IBatchLifecycleService
{
    private readonly STOTOPDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly OrchestrationEngineService _orchestrationEngine;
    private readonly IProgressNotifier _progressNotifier;
    private readonly ILogger<BatchLifecycleService> _logger;

    // 卡片终态集合
    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "completed", "voided", "rejected-final", "error", "cancelled"
    };
    private static readonly HashSet<string> CompletedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "completed"
    };
    private static readonly HashSet<string> ErrorStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "error", "voided", "rejected-final", "cancelled"
    };

    public BatchLifecycleService(
        STOTOPDbContext db,
        IServiceProvider serviceProvider,
        OrchestrationEngineService orchestrationEngine,
        IProgressNotifier progressNotifier,
        ILogger<BatchLifecycleService> logger)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _orchestrationEngine = orchestrationEngine;
        _progressNotifier = progressNotifier;
        _logger = logger;
    }

    public async Task TransitionBatchStatusAsync(CfBatch batch, int newStatus, string? message = null)
    {
        batch.FStatus = newStatus;
        batch.FUpdatedTime = DateTime.Now;
        if (message != null) batch.FErrorMessage = message;
        await _db.SaveChangesAsync();
        // 原推 CardFlowHub org_{orgId} 组为死链（Hub 无加组入口、前端不连），改经 ProgressHub import-{batchId} 组；
        // 递增变更版本号使前端版本保护放行、batch-sync 对账可见。推送失败不影响状态变更
        var version = await BumpChangeVersionAsync(batch);
        try
        {
            await _progressNotifier.NotifyBatchStatusChangedAsync(
                batch.FID, newStatus, message ?? StatusText(newStatus), null, version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR推送BatchStatusChanged失败, BatchId={BatchId}", batch.FID);
        }
    }

    public async Task RefreshBatchStatusAsync(long batchId)
    {
        var batch = await _db.Set<CfBatch>().FirstOrDefaultAsync(x => x.FID == batchId);
        if (batch == null)
        {
            _logger.LogWarning("RefreshBatchStatusAsync: 批次 {BatchId} 不存在", batchId);
            return;
        }

        // 仅在批次进入 fan-out 之后聚合
        if (batch.FStatus < 3)
        {
            _logger.LogDebug("批次 {BatchId} 当前状态={Status}, 未进入 fan-out 阶段，跳过聚合", batchId, batch.FStatus);
            return;
        }

        // 已撤销批次不再聚合
        if (batch.FIsRevoked)
        {
            return;
        }

        var cardStatuses = await _db.Set<CfCard>()
            .Where(c => c.FBatchId == batchId)
            .Select(c => c.FStatus)
            .ToListAsync();

        if (cardStatuses.Count == 0)
        {
            _logger.LogDebug("批次 {BatchId} 无关联卡片，跳过聚合", batchId);
            return;
        }

        int completed = cardStatuses.Count(s => CompletedStatuses.Contains(s));
        int errored = cardStatuses.Count(s => ErrorStatuses.Contains(s));
        int terminal = cardStatuses.Count(s => TerminalStatuses.Contains(s));
        int active = cardStatuses.Count - terminal;

        // 状态聚合规则：
        // 全部 Completed         → 5 已完成
        // 全部终态（含 Error 等） → 5 已完成
        // 任何卡片非终态        → 4 处理中
        var previousStatus = batch.FStatus;
        if (active == 0)
        {
            batch.FStatus = 5;
        }
        else
        {
            batch.FStatus = 4;
        }

        batch.FSuccessRows = completed;
        batch.FFailedRows = errored;
        batch.FUpdatedTime = DateTime.Now;

        await _db.SaveChangesAsync();
        _logger.LogDebug("批次 {BatchId} 聚合完成: status={Status}, success={Success}, failed={Failed}",
            batchId, batch.FStatus, completed, errored);

        // SignalR 推送批次状态变更（同 TransitionBatchStatusAsync：经 ProgressHub import-{batchId} 组 + 版本号）
        var version = await BumpChangeVersionAsync(batch);
        try
        {
            await _progressNotifier.NotifyBatchStatusChangedAsync(
                batch.FID, batch.FStatus, StatusText(batch.FStatus), null, version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR推送BatchStatusChanged失败(聚合), BatchId={BatchId}", batch.FID);
        }

        // 批次刚刚进入完成状态（从非5跳变5），触发编排回调
        if (previousStatus != 5 && batch.FStatus == 5)
        {
            await NotifyOrchestrationOnBatchCompletedAsync(batch);
        }
    }

    public async Task RevokeBatchAsync(long batchId, long operatorId)
    {
        // 撤销权威路径收敛在 BatchRevokeHandler（WorkItem/撤销日志/事件 + 本服务的级联段）。
        // handler 构造注入本服务，这里须经 IServiceProvider 懒解析避免构造环。
        var handler = _serviceProvider.GetRequiredService<BatchRevokeHandler>();
        await handler.RevokeBatchAsync(batchId, operatorId, force: false);
    }

    public async Task CascadeCancelBatchArtifactsAsync(long batchId)
    {
        // 1. 级联取消所有未完成卡片（含凭证红冲）。
        // 全局默认 NoTracking，改状态须 AsTracking 才能落库
        var cards = await _db.Set<CfCard>()
            .AsTracking()
            .Where(c => c.FBatchId == batchId)
            .ToListAsync();

        foreach (var card in cards)
        {
            if (TerminalStatuses.Contains(card.FStatus)) continue;

            await TryReverseCardVouchersAsync(card);

            card.FStatus = "cancelled";
            card.FUpdatedTime = DateTime.Now;
            card.FCompletedTime = DateTime.Now;

            // 编排回调：通知编排引擎卡片已取消
            await NotifyOrchestrationOnCardCompletedAsync(card);
        }

        // 2. 级联更新所有 CfBatchRow 状态为 5(已撤销)，排除状态 4(已忽略)
        var rows = await _db.Set<CfBatchRow>()
            .AsTracking()
            .Where(r => r.FBatchId == batchId && r.FStatus != 4)
            .ToListAsync();
        foreach (var r in rows)
        {
            r.FStatus = 5; // 已撤销
            r.FUpdatedTime = DateTime.Now;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("批次 {BatchId} 撤销级联完成：卡片 {CardCount} 张 / 行明细 {RowCount} 行",
            batchId, cards.Count, rows.Count);
    }

    public async Task<BatchProgressDto> GetBatchProgressAsync(long batchId)
    {
        var batch = await _db.Set<CfBatch>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.FID == batchId)
            ?? throw new InvalidOperationException($"批次 {batchId} 不存在");

        var cards = await _db.Set<CfCard>().AsNoTracking()
            .Where(c => c.FBatchId == batchId)
            .Select(c => new { c.FID, c.FStatus, c.FCurrentStageInstanceId })
            .ToListAsync();

        int completed = cards.Count(c => CompletedStatuses.Contains(c.FStatus));
        int failed = cards.Count(c => ErrorStatuses.Contains(c.FStatus));

        // 活跃卡片按当前节点分组
        var activeCardIds = cards
            .Where(c => !TerminalStatuses.Contains(c.FStatus) && c.FCurrentStageInstanceId.HasValue)
            .Select(c => c.FCurrentStageInstanceId!.Value)
            .Distinct()
            .ToList();

        var stageNameMap = await _db.Set<CfStageInstance>().AsNoTracking()
            .Where(s => activeCardIds.Contains(s.FID))
            .Select(s => new { s.FID, s.FStageName })
            .ToDictionaryAsync(s => s.FID, s => s.FStageName);

        var stageProgress = new Dictionary<string, int>();
        foreach (var card in cards)
        {
            if (TerminalStatuses.Contains(card.FStatus)) continue;
            string nodeName = "未开始";
            if (card.FCurrentStageInstanceId.HasValue
                && stageNameMap.TryGetValue(card.FCurrentStageInstanceId.Value, out var name)
                && !string.IsNullOrWhiteSpace(name))
            {
                nodeName = name;
            }
            if (!stageProgress.ContainsKey(nodeName)) stageProgress[nodeName] = 0;
            stageProgress[nodeName]++;
        }

        return new BatchProgressDto
        {
            BatchId = batchId,
            TotalCards = cards.Count,
            StageProgress = stageProgress,
            Completed = completed,
            Failed = failed,
            BatchStatus = batch.FStatus,
            IsRevoked = batch.FIsRevoked
        };
    }

    /// <summary>
    /// 递增批次变更版本号（SEQ_BatchChange 全局单调，与 CfImportController 同一序列）。
    /// 前端 applyUpdate 按版本号丢弃过时消息，且 batch-sync 对账按 FChangeVersion 水位取增量，
    /// 不递增则推送被前端版本保护静默丢弃。失败（如 InMemory 测试库无 SEQUENCE）返回 0，前端靠轮询恢复。
    /// </summary>
    public async Task<long> BumpChangeVersionAsync(CfBatch batch)
    {
        try
        {
            var versions = await _db.Database
                .SqlQueryRaw<long>(
                    "UPDATE [CF批次] SET [F变更版本号] = NEXT VALUE FOR dbo.SEQ_BatchChange OUTPUT INSERTED.[F变更版本号] WHERE [FID] = {0}",
                    batch.FID)
                .ToListAsync();
            batch.FChangeVersion = versions.FirstOrDefault();

            // 关键并发修复(真实文件E2E暴露)：本方法用 raw SQL 直接 UPDATE 该批次行 → DB 端 F乐观锁(rowversion) 已递增，
            // 但被追踪的 batch 实体仍持旧 token。若不同步，批次链后续任何 SaveChanges 连带保存本批次会 WHERE 旧token → 影响0行
            // → DbUpdateConcurrencyException，导致批次在导入后中断、autoVoucher 等后续节点永不执行(极兔 import→autoVoucher 直连
            // 无中间质量节点故首次触发；申通有质量节点会 Reload 批次而躲过)。ReloadAsync 重取本行、刷新 token 与各字段(此刻批次
            // 变更已在调用方 SaveChanges 落库、实体为 Unchanged，Reload 无数据丢失)。
            var entry = _db.Entry(batch);
            if (entry.State != EntityState.Detached)
            {
                try { await entry.ReloadAsync(); }
                catch (Exception rex) { _logger.LogWarning(rex, "BumpChangeVersion 后 Reload 批次刷新乐观锁失败, BatchId={BatchId}", batch.FID); }
            }
            return batch.FChangeVersion;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取 SEQ_BatchChange 版本号失败, BatchId={BatchId}", batch.FID);
            return 0;
        }
    }

    /// <summary>CfBatch 状态码 → 中文状态文案（推送 statusText，前端终态时作摘要/错误信息展示）</summary>
    private static string StatusText(int status) => status switch
    {
        CfBatchStatus.Parsing => "解析中",
        CfBatchStatus.Staged => "已暂存",
        CfBatchStatus.QualityChecking => "质检中",
        CfBatchStatus.CardCreated => "已创建卡片",
        CfBatchStatus.Processing => "处理中",
        CfBatchStatus.Completed => "已完成",
        CfBatchStatus.Failed => "失败",
        CfBatchStatus.PartiallyCompleted => "部分完成",
        CfBatchStatus.Revoked => "已撤销",
        _ => $"状态{status}"
    };

    /// <summary>
    /// 批次进入完成状态时通知编排引擎（如该批次由编排实例驱动产生）。
    /// </summary>
    private async Task NotifyOrchestrationOnBatchCompletedAsync(CfBatch batch)
    {
        if (batch.FOrchestrationInstanceId == null || string.IsNullOrEmpty(batch.FOrchestrationNodeId))
            return;

        try
        {
            await _orchestrationEngine.OnBatchCompletedAsync(
                batch.FOrchestrationInstanceId.Value,
                batch.FOrchestrationNodeId!,
                batch.FID,  // 传入批次 ID
                ExtractBatchResultData(batch));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "编排回调失败(批次): 编排实例={InstanceId}, 节点={NodeId}, 批次={BatchId}",
                batch.FOrchestrationInstanceId, batch.FOrchestrationNodeId, batch.FID);
        }
    }

    /// <summary>
    /// 卡片被批次撤销进入 cancelled 终态时通知编排引擎。
    /// </summary>
    private async Task NotifyOrchestrationOnCardCompletedAsync(CfCard card)
    {
        if (card.FOrchestrationInstanceId == null || string.IsNullOrEmpty(card.FOrchestrationNodeId))
            return;

        try
        {
            JsonElement? resultData = null;
            if (!string.IsNullOrEmpty(card.FDataJson))
            {
                try
                {
                    resultData = JsonDocument.Parse(card.FDataJson).RootElement.Clone();
                }
                catch (JsonException)
                {
                    resultData = null;
                }
            }

            await _orchestrationEngine.OnFlowCompletedAsync(
                card.FOrchestrationInstanceId.Value,
                card.FOrchestrationNodeId!,
                card.FID,
                card.FStatus,
                resultData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "编排回调失败(卡片): 编排实例={InstanceId}, 节点={NodeId}, 卡片={CardId}",
                card.FOrchestrationInstanceId, card.FOrchestrationNodeId, card.FID);
        }
    }

    /// <summary>
    /// 提取批次结果摘要作为编排回调 resultData。
    /// </summary>
    private static JsonElement? ExtractBatchResultData(CfBatch batch)
    {
        var result = new
        {
            totalRows = batch.FTotalRows,
            successRows = batch.FSuccessRows,
            failedRows = batch.FFailedRows
        };
        return JsonSerializer.SerializeToElement(result);
    }

    /// <summary>
    /// 检查卡片 FDataJson 中是否含 voucher1Ref / voucher2Ref，若有则触发凭证红冲
    /// </summary>
    private async Task TryReverseCardVouchersAsync(CfCard card)
    {
        if (string.IsNullOrWhiteSpace(card.FDataJson)) return;

        var voucherIds = ExtractVoucherRefs(card.FDataJson);
        if (voucherIds.Count == 0) return;

        // IVoucherService 可能未注册（独立运行单测场景），按需解析
        var voucherService = _serviceProvider.GetService<IVoucherService>();
        if (voucherService == null)
        {
            _logger.LogWarning("卡片 {CardId} 含凭证引用 {Count} 个，但 IVoucherService 未注册，跳过红冲",
                card.FID, voucherIds.Count);
            return;
        }

        foreach (var vid in voucherIds)
        {
            try
            {
                await voucherService.CreateReversalAsync(vid);
                _logger.LogInformation("卡片 {CardId} 凭证 {VoucherId} 已红冲", card.FID, vid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "卡片 {CardId} 凭证 {VoucherId} 红冲失败", card.FID, vid);
            }
        }
    }

    /// <summary>从 FDataJson 提取 voucher1Ref / voucher2Ref 等长整型 ID</summary>
    private static List<long> ExtractVoucherRefs(string dataJson)
    {
        var result = new List<long>();
        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!prop.Name.StartsWith("voucher", StringComparison.OrdinalIgnoreCase)) continue;
                if (!prop.Name.EndsWith("Ref", StringComparison.OrdinalIgnoreCase)) continue;
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.Number when prop.Value.TryGetInt64(out var n) && n > 0:
                        result.Add(n);
                        break;
                    case JsonValueKind.String when long.TryParse(prop.Value.GetString(), out var s) && s > 0:
                        result.Add(s);
                        break;
                }
            }
        }
        catch (JsonException)
        {
            // 忽略解析错误
        }
        return result;
    }
}
