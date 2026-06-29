using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Enums;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Services;

public class RevokeService : IRevokeService
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<RevokeService> _logger;
    private readonly IEnumerable<IDataScopeRevokeHandler> _dataScopeRevokeHandlers;

    public RevokeService(
        STOTOPDbContext db,
        ILogger<RevokeService> logger,
        IEnumerable<IDataScopeRevokeHandler> dataScopeRevokeHandlers)
    {
        _db = db;
        _logger = logger;
        _dataScopeRevokeHandlers = dataScopeRevokeHandlers;
    }

    public async Task<RevokeResultDto> RevokeStepAsync(long workItemId, long operatorId, string operatorName)
    {
        var workItem = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId);
        if (workItem == null)
        {
            _logger.LogWarning("工作项 {WorkItemId} 不存在，跳过撤销处理", workItemId);
            return new RevokeResultDto
            {
                Success = false,
                ErrorMessage = $"工作项 {workItemId} 不存在"
            };
        }

        if (!await CanRevokeAsync(workItemId))
        {
            return new RevokeResultDto
            {
                Success = false,
                ErrorMessage = "当前工作项不可撤销（存在后续依赖）"
            };
        }

        var result = new RevokeResultDto { Success = true };

        // 框架性撤销逻辑：根据 DataScopeId 标记相关数据
        // 具体撤销策略在 Pipeline 集成时按业务场景完善
        var revokeLog = new WfRevokeLog
        {
            FOrgId = workItem.FOrgId,
            FChainId = workItem.FChainId,
            FDataScopeId = workItem.FDataScopeId,
            FOperatorId = operatorId,
            FOperatorName = operatorName,
            FRevokeType = "Step",
            FTargetTable = null, // 具体表在 Pipeline 集成时确定
            FAffectedRows = 0,
            FRevokeStrategy = "MarkDeleted",
            FIsSuccess = true
        };

        _db.Set<WfRevokeLog>().Add(revokeLog);

        // 更新工作项状态为已取消
        var oldStatus = workItem.FStatus;
        workItem.FStatus = (int)WorkItemStatus.Cancelled;
        workItem.FUpdateTime = DateTime.Now;
        workItem.FRemark = $"已撤销（操作人: {operatorName}）";

        // 写操作日志
        _db.Set<WfWorkItemLog>().Add(new WfWorkItemLog
        {
            FWorkItemId = workItemId,
            FOperatorId = operatorId,
            FOperatorName = operatorName,
            FAction = "Revoked",
            FFromStatus = oldStatus,
            FToStatus = (int)WorkItemStatus.Cancelled,
            FContent = "步骤级撤销"
        });

        await _db.SaveChangesAsync();

        // 触发业务数据撤销（暂存表 + 凭证）
        var businessAffected = await ExecuteDataScopeRevokeAsync(workItem.FDataScopeId, operatorId);
        revokeLog.FAffectedRows = businessAffected;

        result.Logs.Add(new RevokeLogDto
        {
            Id = revokeLog.FID,
            ChainId = revokeLog.FChainId,
            RevokeType = revokeLog.FRevokeType,
            TargetTable = revokeLog.FTargetTable,
            AffectedRows = revokeLog.FAffectedRows,
            Strategy = revokeLog.FRevokeStrategy,
            IsSuccess = revokeLog.FIsSuccess,
            CreateTime = revokeLog.FCreateTime
        });

        _logger.LogInformation("步骤级撤销完成，工作项: {WorkItemId}，操作人: {Operator}", workItemId, operatorName);
        return result;
    }

    public async Task<RevokeResultDto> RevokeChainAsync(string chainId, long operatorId, string operatorName)
    {
        var workItems = await _db.Set<WfWorkItem>()
            .Where(w => w.FChainId == chainId)
            .OrderByDescending(w => w.FChainSeq)
            .ToListAsync();

        if (workItems.Count == 0)
        {
            return new RevokeResultDto
            {
                Success = false,
                ErrorMessage = $"链路 {chainId} 下没有工作项"
            };
        }

        var result = new RevokeResultDto { Success = true };
        var now = DateTime.Now;
        var orgId = workItems.First().FOrgId;

        foreach (var workItem in workItems)
        {
            // 撤销每个工作项
            workItem.FStatus = (int)WorkItemStatus.Cancelled;
            workItem.FUpdateTime = now;
            workItem.FRemark = $"链路级撤销（操作人: {operatorName}）";

            var revokeLog = new WfRevokeLog
            {
                FOrgId = orgId,
                FChainId = chainId,
                FDataScopeId = workItem.FDataScopeId,
                FOperatorId = operatorId,
                FOperatorName = operatorName,
                FRevokeType = "Chain",
                FTargetTable = null,
                FAffectedRows = 0,
                FRevokeStrategy = "MarkDeleted",
                FIsSuccess = true
            };

            _db.Set<WfRevokeLog>().Add(revokeLog);

            _db.Set<WfWorkItemLog>().Add(new WfWorkItemLog
            {
                FWorkItemId = workItem.FID,
                FOperatorId = operatorId,
                FOperatorName = operatorName,
                FAction = "Revoked",
                FContent = "链路级撤销"
            });
        }

        await _db.SaveChangesAsync();

        // 触发业务数据撤销：对每个有 DataScopeId 的工作项执行撤销
        var dataScopeIds = workItems
            .Where(w => !string.IsNullOrEmpty(w.FDataScopeId))
            .Select(w => w.FDataScopeId!)
            .Distinct()
            .ToList();

        int totalBusinessAffected = 0;
        foreach (var dsId in dataScopeIds)
        {
            totalBusinessAffected += await ExecuteDataScopeRevokeAsync(dsId, operatorId);
        }

        _logger.LogInformation("链路级撤销完成，链路: {ChainId}，涉及 {Count} 个工作项，业务数据影响 {BusinessRows} 行，操作人: {Operator}",
            chainId, workItems.Count, totalBusinessAffected, operatorName);
        result.TotalAffectedRows = workItems.Count + totalBusinessAffected;
        return result;
    }

    public async Task<List<RevokeLogDto>> GetRevokeLogsAsync(string? chainId = null, string? dataScopeId = null)
    {
        var query = _db.Set<WfRevokeLog>().AsQueryable();

        if (!string.IsNullOrEmpty(chainId))
            query = query.Where(l => l.FChainId == chainId);
        if (!string.IsNullOrEmpty(dataScopeId))
            query = query.Where(l => l.FDataScopeId == dataScopeId);

        var logs = await query
            .OrderByDescending(l => l.FCreateTime)
            .Take(100)
            .ToListAsync();

        return logs.Select(l => new RevokeLogDto
        {
            Id = l.FID,
            ChainId = l.FChainId,
            RevokeType = l.FRevokeType,
            TargetTable = l.FTargetTable,
            AffectedRows = l.FAffectedRows,
            Strategy = l.FRevokeStrategy,
            IsSuccess = l.FIsSuccess,
            CreateTime = l.FCreateTime
        }).ToList();
    }

    public async Task<bool> CanRevokeAsync(long workItemId)
    {
        var workItem = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId);
        if (workItem == null) return false;

        // 已取消的不可再撤销
        if (workItem.FStatus == (int)WorkItemStatus.Cancelled)
            return false;

        // 检查是否有后续子工作项已完成（有依赖则不可撤）
        if (!string.IsNullOrEmpty(workItem.FChainId))
        {
            var hasCompletedSubsequent = await _db.Set<WfWorkItem>()
                .AnyAsync(w => w.FChainId == workItem.FChainId
                    && w.FChainSeq > workItem.FChainSeq
                    && w.FStatus == (int)WorkItemStatus.Completed);

            if (hasCompletedSubsequent)
                return false;
        }

        return true;
    }

    /// <summary>执行 DataScope 级业务数据撤销，尽力执行不阻断</summary>
    private async Task<int> ExecuteDataScopeRevokeAsync(string? dataScopeId, long operatorId)
    {
        if (string.IsNullOrEmpty(dataScopeId))
            return 0;

        int totalAffected = 0;

        foreach (var handler in _dataScopeRevokeHandlers)
        {
            try
            {
                var affected = await handler.RevokeByDataScopeAsync(dataScopeId, operatorId);
                totalAffected += affected;
                _logger.LogInformation(
                    "撤销Handler执行完成: {Handler}，DataScopeId={DataScopeId}，影响{Rows}行",
                    handler.HandlerName, dataScopeId, affected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "撤销Handler执行失败: {Handler}，DataScopeId={DataScopeId}",
                    handler.HandlerName, dataScopeId);
                // 尽力执行，不阻断后续 Handler
            }
        }

        return totalAffected;
    }
}
