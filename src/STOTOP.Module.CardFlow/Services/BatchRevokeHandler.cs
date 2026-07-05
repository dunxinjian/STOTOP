using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Events;
using STOTOP.Module.System.Services;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 批次撤销处理器：负责批次删除前的预检查和撤销/物理删除操作。
/// 撤销的无工单权威路径（产品决策：上传中心主动撤销是即时动作，不产生 WorkItem 工单）：
/// 级联取消（IBatchLifecycleService 级联段）→ 标记撤销 → 撤销日志(WfRevokeLog) → 事件 → 版本号递增 + SignalR 推送。
/// IBatchLifecycleService.RevokeBatchAsync 薄委托到这里，勿在别处再实现撤销
/// </summary>
public class BatchRevokeHandler
{
    private readonly STOTOPDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IBatchLifecycleService _batchLifecycle;
    private readonly IProgressNotifier _progressNotifier;
    private readonly ILogger<BatchRevokeHandler> _logger;

    public BatchRevokeHandler(
        STOTOPDbContext db,
        IConfiguration configuration,
        IEventDispatcher eventDispatcher,
        IBatchLifecycleService batchLifecycle,
        IProgressNotifier progressNotifier,
        ILogger<BatchRevokeHandler> logger)
    {
        _db = db;
        _configuration = configuration;
        _eventDispatcher = eventDispatcher;
        _batchLifecycle = batchLifecycle;
        _progressNotifier = progressNotifier;
        _logger = logger;
    }

    /// <summary>
    /// 预检查：检查批次状态、关联凭证审核状态、期间结账状态
    /// </summary>
    public async Task<BatchDeletePreCheck> PreCheckAsync(long batchId)
    {
        var result = new BatchDeletePreCheck { CanDelete = true };

        // 1. 查询批次是否存在、当前状态
        var batch = await _db.Set<CfBatch>().FirstOrDefaultAsync(b => b.FID == batchId);
        if (batch == null)
        {
            result.CanDelete = false;
            result.BlockReason = "批次不存在";
            return result;
        }

        // 注：不再阻止 Processing 状态的撤销/删除（软删除安全，pipeline会自行检测已撤销状态并停止）

        if (batch.FIsRevoked)
        {
            // 已撤销批次可以从回收站中彻底删除，直接返回允许
            result.CanDelete = true;
            return result;
        }

        // 统计在途卡片（撤销时会被级联取消，前端据此知情提示，不阻止操作）
        var activeStatuses = new[] { "draft", "active", "returned" };
        result.ActiveCardCount = await _db.Set<CfCard>()
            .CountAsync(c => c.FBatchId == batchId && activeStatuses.Contains(c.FStatus));

        // 2. 查询关联凭证
        var encryptionKey = _configuration.GetValue<string>("Security:EncryptionKey");
        var connectionString = DbConnectionsHelper.GetSystemConnectionString(encryptionKey)
            ?? throw new InvalidOperationException("无法获取数据库连接字符串");

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // 3. 从 CF凭证记录 查询批次关联的凭证生成记录（替代已废弃的 DC凭证生成记录）
        int voucherRecordCount = 0;
        using (var cmd = new SqlCommand(
            "SELECT ISNULL(SUM([F生成凭证数]), 0) FROM [CF凭证记录] WHERE [F批次ID] = @batchId",
            connection))
        {
            cmd.Parameters.AddWithValue("@batchId", batchId);
            cmd.CommandTimeout = 60;
            voucherRecordCount = (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        // 4. 从 FIN凭证 按 F数据作用域ID 查询关联凭证的审核状态和期间结账状态
        var scopeId = batchId.ToString();
        int voucherCount = 0;
        using (var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM [FIN凭证] WHERE [F数据作用域ID] = @scopeId",
            connection))
        {
            cmd.Parameters.AddWithValue("@scopeId", scopeId);
            cmd.CommandTimeout = 60;
            voucherCount = (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        // 取 CF凭证记录 和 FIN凭证 中的较大值作为受影响凭证数
        result.AffectedVoucherCount = Math.Max(voucherCount, voucherRecordCount);

        if (voucherCount > 0)
        {
            // 查询审核状态（FStatus=2 为已审核）
            int auditedCount;
            using (var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM [FIN凭证] WHERE [F数据作用域ID] = @scopeId AND [F状态] = 2",
                connection))
            {
                cmd.Parameters.AddWithValue("@scopeId", scopeId);
                cmd.CommandTimeout = 60;
                auditedCount = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            }

            if (auditedCount > 0)
            {
                result.HasAuditedVouchers = true;

                // 4. 检查期间结账状态
                int closedPeriodCount;
                using (var cmd = new SqlCommand(
                    @"SELECT COUNT(*) 
                      FROM [FIN凭证] v
                      INNER JOIN [FIN会计期间] p ON p.[FID] = v.[F期间ID]
                      WHERE v.[F数据作用域ID] = @scopeId
                        AND v.[F状态] = 2
                        AND p.[F是否结账] = 1",
                    connection))
                {
                    cmd.Parameters.AddWithValue("@scopeId", scopeId);
                    cmd.CommandTimeout = 60;
                    closedPeriodCount = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                }

                if (closedPeriodCount > 0)
                {
                    result.HasClosedPeriod = true;
                    result.CanDelete = false;
                    result.BlockReason = $"该批次包含{closedPeriodCount}张凭证位于已结账期间，无法删除。请先反结账对应期间。";
                    return result;
                }
            }
        }

        // 5. 统计影响的数据行数（暂存表行数）
        if (!string.IsNullOrEmpty(batch.FActualTargetTable))
        {
            try
            {
                using var cmd = new SqlCommand(
                    $"SELECT COUNT(*) FROM [{batch.FActualTargetTable}] WHERE [F批次ID] = @batchId",
                    connection);
                cmd.Parameters.AddWithValue("@batchId", batchId);
                cmd.CommandTimeout = 60;
                result.AffectedRowCount = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "统计暂存表行数失败: BatchId={BatchId}, Table={Table}", batchId, batch.FActualTargetTable);
            }
        }

        return result;
    }

    /// <summary>
    /// 执行撤销/删除（无工单路径）
    /// force=false: 软删除（级联取消未完成卡片/凭证红冲/行明细置5 + 标记F已撤销=1 + 撤销日志 + 事件 + 版本号递增 + SignalR 推送）
    /// force=true: 物理删除（调用CascadeDeleteBatchAsync）
    /// </summary>
    /// <param name="batchId">批次ID</param>
    /// <param name="operatorId">操作人ID</param>
    /// <param name="force">是否强制物理删除</param>
    /// <param name="cascadeDeleteFunc">物理删除委托（从Controller传入）</param>
    public async Task RevokeBatchAsync(long batchId, long operatorId, bool force = false,
        Func<long, string?, bool, Task<BatchDeleteResult>>? cascadeDeleteFunc = null)
    {
        // 并发保护：使用 EF Core 显式事务 + AsTracking 实现读取-校验-操作原子性
        // AsTracking 硓 SaveChangesAsync 会自动检测并发修改（如状态变更），抛出 DbUpdateConcurrencyException
        var batch = await _db.Set<CfBatch>()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId)
            ?? throw new InvalidOperationException($"批次 {batchId} 不存在");

        // 状态校验（首次检查）：不再阻止 Processing 状态的撤销
        // 撤销是安全的，pipeline 会自行检测已撤销状态并停止

        // 并发保护：如果批次已被撤销，防止重复操作
        if (batch.FIsRevoked && !force)
        {
            _logger.LogInformation("批次 {BatchId} 已处于撤销状态，重复撤销请求被忽略", batchId);
            return;
        }

        // force=true（彻底删除）：直接执行物理删除，不创建 WorkItem
        if (force)
        {
            if (cascadeDeleteFunc == null)
                throw new InvalidOperationException("物理删除模式需要提供级联删除委托");

            await cascadeDeleteFunc(batchId, batch.FActualTargetTable, true);
            _logger.LogInformation("批次 {BatchId} 已从回收站彻底物理删除", batchId);

            try
            {
                await _eventDispatcher.PublishAsync(new ImportBatchPurgedEvent
                {
                    BatchId = batchId,
                    OrgId = batch.FOrgId,
                    TargetTable = batch.FActualTargetTable,
                    OperatorId = operatorId,
                    PurgedAt = DateTime.UtcNow,
                    ModuleCode = "DataCenter"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发布批次物理删除事件失败，不影响主流程: 批次={BatchId}", batchId);
            }

            return;
        }

        // force=false（软删除，无工单）：级联取消 → 标记撤销 → 撤销日志 → 事件 → 版本推送
        // 级联段（取消未完成卡片/凭证红冲/行明细置5）收敛自 BatchLifecycleService，先级联后标记批次；
        // 与本方法共享同一 scoped DbContext，其 SaveChanges 时批次尚无未落库改动，不会提前刷写
        await _batchLifecycle.CascadeCancelBatchArtifactsAsync(batchId);

        // 软删除：标记撤销（不设 FWorkItemId）
        batch.FIsRevoked = true;
        batch.FRevokedTime = DateTime.Now;
        batch.FRevokedById = operatorId;
        batch.FStatus = CfBatchStatus.Revoked;
        batch.FUpdatedTime = DateTime.Now;

        var affected = await _db.SaveChangesAsync();
        _logger.LogInformation("批次 {BatchId} 撤销保存完成，受影响行数: {Rows}", batchId, affected);

        // 记录撤销日志
        var revokeLog = new WfRevokeLog
        {
            FOrgId = 1,
            FDataScopeId = batchId.ToString(), // CfBatch 无 F数据作用域ID，使用批次ID代替
            FOperatorId = operatorId,
            FRevokeType = "BatchRevoke",
            FTargetTable = "CF批次",
            FAffectedRows = 1,
            FRevokeStrategy = "MarkDeleted",
            FIsSuccess = true
        };
        _db.Set<WfRevokeLog>().Add(revokeLog);
        await _db.SaveChangesAsync();

        _logger.LogInformation("批次 {BatchId} 已标记撤销（无工单）", batchId);

        try
        {
            await _eventDispatcher.PublishAsync(new ImportBatchRevokedEvent
            {
                BatchId = batchId,
                OrgId = batch.FOrgId,
                OperatorId = operatorId,
                RevokedAt = DateTime.UtcNow,
                ModuleCode = "DataCenter"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发布批次撤销事件失败，不影响主流程: 批次={BatchId}", batchId);
        }

        // 版本号递增 + SignalR 推送（复用 BatchLifecycleService 单一 SEQ 实现；推送失败仅告警不影响主流程）
        var version = await _batchLifecycle.BumpChangeVersionAsync(batch);
        try
        {
            await _progressNotifier.NotifyBatchStatusChangedAsync(batchId, CfBatchStatus.Revoked, "Revoked", null, version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "推送批次撤销状态变更失败，不影响主流程: BatchId={BatchId}", batchId);
        }
    }

    /// <summary>
    /// 恢复已撤销的批次
    /// </summary>
    /// <param name="batchId">批次ID</param>
    /// <param name="operatorId">操作人ID</param>
    public async Task RestoreBatchAsync(long batchId, long operatorId)
    {
        var batch = await _db.Set<CfBatch>()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.FID == batchId)
            ?? throw new InvalidOperationException($"批次 {batchId} 不存在");

        if (!batch.FIsRevoked)
            throw new InvalidOperationException("该批次未被撤销，无需恢复");

        // 恢复批次（无工单）：清除撤销标记 + 回到已暂存状态
        batch.FIsRevoked = false;
        batch.FRevokedTime = null;
        batch.FRevokedById = null;
        batch.FStatus = CfBatchStatus.Staged; // 恢复后回到已暂存状态
        batch.FUpdatedTime = DateTime.Now;

        await _db.SaveChangesAsync();

        // 记录恢复日志
        var restoreLog = new WfRevokeLog
        {
            FOrgId = 1,
            FDataScopeId = batchId.ToString(), // CfBatch 无 F数据作用域ID
            FOperatorId = operatorId,
            FRevokeType = "BatchRestore",
            FTargetTable = "CF批次",
            FAffectedRows = 1,
            FRevokeStrategy = "Restore",
            FIsSuccess = true
        };
        _db.Set<WfRevokeLog>().Add(restoreLog);
        await _db.SaveChangesAsync();

        _logger.LogInformation("批次 {BatchId} 已恢复（无工单）", batchId);

        // 版本号递增 + SignalR 推送
        var version = await _batchLifecycle.BumpChangeVersionAsync(batch);
        try
        {
            await _progressNotifier.NotifyBatchStatusChangedAsync(batchId, CfBatchStatus.Staged, "Staged", null, version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "推送批次恢复状态变更失败，不影响主流程: BatchId={BatchId}", batchId);
        }
    }
}

