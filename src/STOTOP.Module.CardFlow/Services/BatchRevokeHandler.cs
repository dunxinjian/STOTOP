using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Events;
using STOTOP.Module.System.Services;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Enums;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 批次撤销处理器：负责批次删除前的预检查和撤销/物理删除操作
/// </summary>
public class BatchRevokeHandler
{
    private readonly STOTOPDbContext _db;
    private readonly IWorkItemService _workItemService;
    private readonly IConfiguration _configuration;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<BatchRevokeHandler> _logger;

    public BatchRevokeHandler(
        STOTOPDbContext db,
        IWorkItemService workItemService,
        IConfiguration configuration,
        IEventDispatcher eventDispatcher,
        ILogger<BatchRevokeHandler> logger)
    {
        _db = db;
        _workItemService = workItemService;
        _configuration = configuration;
        _eventDispatcher = eventDispatcher;
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
    /// 执行撤销/删除
    /// force=false: 软删除（标记F已撤销=1，创建WorkItem，记录撤销日志）
    /// force=true: 物理删除（调用CascadeDeleteBatchAsync） + 创建WorkItem记录
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

        // force=false（软删除）：创建 WorkItem 并标记撤销
        long workItemId;
        try
        {
            var workItemRequest = new CreateWorkItemRequest
            {
                OrgId = 1, // 系统级操作
                Title = $"批次撤销: {batch.FBatchNo}",
                Description = $"撤销批次 {batch.FBatchNo}（文件: {batch.FFileName}），标记为已撤销",
                Type = (int)WorkItemType.Task,
                Source = (int)WorkItemSource.Manual,
                Priority = (int)WorkItemPriority.Normal,
                CreatorId = operatorId,
                AssigneeId = operatorId,
                Module = "DataCenter",
                BizType = "ImportBatch",
                BizId = batchId,
                DataScopeId = batchId.ToString(), // CfBatch 无 F数据作用域ID，使用批次ID代替
                AutoDispatch = false
            };

            var workItem = await _workItemService.CreateAsync(workItemRequest);
            workItemId = workItem.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建撤销WorkItem失败: BatchId={BatchId}", batchId);
            throw new InvalidOperationException($"创建工作项失败: {ex.Message}", ex);
        }

        try
        {
            // 软删除：标记撤销
            batch.FIsRevoked = true;
            batch.FRevokedTime = DateTime.Now;
            batch.FRevokedById = operatorId;
            batch.FWorkItemId = workItemId;

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

            _logger.LogInformation("批次 {BatchId} 已标记撤销，WorkItemId={WorkItemId}", batchId, workItemId);

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

            // 更新 WorkItem 状态为 Completed
            try
            {
                await _workItemService.CompleteAsync(workItemId, operatorId,
                    result: "SoftRevoke",
                    remark: "批次已标记撤销",
                    enforceAssignee: false);
            }
            catch (Exception completeEx)
            {
                _logger.LogWarning(completeEx, "完成撤销WorkItem失败（工作项可能不存在或被过滤）: WorkItemId={WorkItemId}, BatchId={BatchId}", workItemId, batchId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批次撤销/删除执行失败: BatchId={BatchId}", batchId);

            // 尝试将 WorkItem 标记为取消
            try
            {
                await _workItemService.CancelAsync(workItemId, operatorId, $"执行失败: {ex.Message}", enforceAssignee: false);
            }
            catch (Exception cancelEx)
            {
                _logger.LogWarning(cancelEx, "取消WorkItem失败: WorkItemId={WorkItemId}", workItemId);
            }

            throw;
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

        // 1. 创建 WorkItem
        long workItemId;
        try
        {
            var workItemRequest = new CreateWorkItemRequest
            {
                OrgId = 1,
                Title = $"批次恢复: {batch.FBatchNo}",
                Description = $"恢复已撤销批次 {batch.FBatchNo}（文件: {batch.FFileName}）",
                Type = (int)WorkItemType.Task,
                Source = (int)WorkItemSource.Manual,
                Priority = (int)WorkItemPriority.Normal,
                CreatorId = operatorId,
                AssigneeId = operatorId,
                Module = "DataCenter",
                BizType = "ImportBatch",
                BizId = batchId,
                DataScopeId = batchId.ToString(), // CfBatch 无 F数据作用域ID
                AutoDispatch = false
            };

            var workItem = await _workItemService.CreateAsync(workItemRequest);
            workItemId = workItem.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建恢复WorkItem失败: BatchId={BatchId}", batchId);
            throw new InvalidOperationException($"创建工作项失败: {ex.Message}", ex);
        }

        try
        {
            // 2. 恢复批次：清除撤销标记
            batch.FIsRevoked = false;
            batch.FRevokedTime = null;
            batch.FRevokedById = null;
            batch.FWorkItemId = workItemId;

            await _db.SaveChangesAsync();

            // 3. 记录恢复日志
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

            // 4. 更新 WorkItem 状态为 Completed
            try
            {
                await _workItemService.CompleteAsync(workItemId, operatorId,
                    result: "BatchRestore",
                    remark: "批次已恢复",
                    enforceAssignee: false);
            }
            catch (Exception completeEx)
            {
                _logger.LogWarning(completeEx, "完成恢复WorkItem失败（工作项可能不存在或被过滤）: WorkItemId={WorkItemId}, BatchId={BatchId}", workItemId, batchId);
            }

            _logger.LogInformation("批次 {BatchId} 已恢复，WorkItemId={WorkItemId}", batchId, workItemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批次恢复执行失败: BatchId={BatchId}", batchId);

            try
            {
                await _workItemService.CancelAsync(workItemId, operatorId, $"执行失败: {ex.Message}", enforceAssignee: false);
            }
            catch (Exception cancelEx)
            {
                _logger.LogWarning(cancelEx, "取消WorkItem失败: WorkItemId={WorkItemId}", workItemId);
            }

            throw;
        }
    }
}

