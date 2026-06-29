using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Enums;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Services;

public class WorkItemService : IWorkItemService
{
    private readonly STOTOPDbContext _db;
    private readonly IDispatchEngine _dispatchEngine;
    private readonly IWorkItemCallback _callback;
    private readonly IChainService _chainService;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<WorkItemService> _logger;

    public WorkItemService(
        STOTOPDbContext db,
        IDispatchEngine dispatchEngine,
        IWorkItemCallback callback,
        IChainService chainService,
        IEventDispatcher eventDispatcher,
        IWorkHubNotifier workHubNotifier,
        ILogger<WorkItemService> logger)
    {
        _db = db;
        _dispatchEngine = dispatchEngine;
        _callback = callback;
        _chainService = chainService;
        _eventDispatcher = eventDispatcher;
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task<WorkItemDto> CreateAsync(CreateWorkItemRequest request)
    {
        var entity = new WfWorkItem
        {
            FOrgId = request.OrgId,
            FTitle = request.Title,
            FDescription = request.Description,
            FType = request.Type,
            FSource = request.Source,
            FPriority = request.Priority,
            FChainId = request.ChainId,
            FParentWorkItemId = request.ParentWorkItemId,
            FDataScopeId = request.DataScopeId,
            FCreatorId = request.CreatorId,
            FAssigneeId = request.AssigneeId,
            FAssigneeName = request.AssigneeName,
            FModule = request.Module,
            FBizType = request.BizType,
            FBizId = request.BizId,
            FDetailRoute = request.DetailRoute,
            FDeadline = request.Deadline,
            FStatus = (int)WorkItemStatus.Pending
        };

        // 如果有 ChainId，自动计算 ChainSeq
        if (!string.IsNullOrEmpty(request.ChainId))
        {
            var maxSeq = await _db.Set<WfWorkItem>()
                .Where(w => w.FChainId == request.ChainId)
                .MaxAsync(w => (int?)w.FChainSeq) ?? 0;
            entity.FChainSeq = maxSeq + 1;
        }

        _db.Set<WfWorkItem>().Add(entity);
        await _db.SaveChangesAsync();

        // 写入日志
        await WriteLogAsync(entity.FID, request.CreatorId, "Created", null, (int)WorkItemStatus.Pending, $"创建工作项: {request.Title}");

        // 如果 AutoDispatch 则调用 DispatchEngine
        if (request.AutoDispatch && !request.AssigneeId.HasValue)
        {
            try
            {
                await _dispatchEngine.DispatchAsync(entity.FID);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "自动派发失败，工作项 {WorkItemId} 将保持待分配状态", entity.FID);
            }
        }

        // 自动关注链路
        if (!string.IsNullOrEmpty(entity.FChainId))
        {
            await _chainService.AutoFollowParticipantsAsync(entity.FChainId);
        }

        return MapToDto(entity);
    }

    public async Task<WorkItemDto?> GetByIdAsync(long id)
    {
        var entity = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<WorkItemDto?> GetByUidAsync(string uid)
    {
        var entity = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FUID == uid);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<List<WorkItemDto>> GetPendingItemsAsync(long userId, string? module = null)
    {
        var query = _db.Set<WfWorkItem>()
            .Where(w => w.FAssigneeId == userId && w.FStatus == (int)WorkItemStatus.Pending);

        if (!string.IsNullOrEmpty(module))
            query = query.Where(w => w.FModule == module);

        var items = await query
            .OrderByDescending(w => w.FPriority)
            .ThenBy(w => w.FCreateTime)
            .ToListAsync();

        return items.Select(MapToDto).ToList();
    }

    public async Task<List<WorkItemDto>> GetCompletedItemsAsync(long userId, int page = 1, int pageSize = 20)
    {
        var items = await _db.Set<WfWorkItem>()
            .Where(w => w.FAssigneeId == userId && w.FStatus == (int)WorkItemStatus.Completed)
            .OrderByDescending(w => w.FCompletedTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return items.Select(MapToDto).ToList();
    }

    public async Task<List<WorkItemDto>> GetByChainIdAsync(string chainId)
    {
        var items = await _db.Set<WfWorkItem>()
            .Where(w => w.FChainId == chainId)
            .OrderBy(w => w.FChainSeq)
            .ToListAsync();

        return items.Select(MapToDto).ToList();
    }

    public async Task<WorkItemDto> AssignAsync(long workItemId, long assigneeId, string assigneeName)
    {
        var entity = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId)
            ?? throw new InvalidOperationException($"工作项 {workItemId} 不存在");

        var fromStatus = entity.FStatus;
        entity.FAssigneeId = assigneeId;
        entity.FAssigneeName = assigneeName;
        entity.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        await WriteLogAsync(workItemId, assigneeId, "Assigned", fromStatus, entity.FStatus, $"分配给 {assigneeName}");

        // 自动关注链路
        if (!string.IsNullOrEmpty(entity.FChainId))
        {
            await _chainService.AutoFollowParticipantsAsync(entity.FChainId);
        }

        return MapToDto(entity);
    }

    public async Task<WorkItemDto> StartAsync(long workItemId, long operatorId, bool enforceAssignee = true)
    {
        var entity = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId)
            ?? throw new InvalidOperationException($"工作项 {workItemId} 不存在");

        EnsureAssignee(entity, operatorId, enforceAssignee);

        if (entity.FStatus != (int)WorkItemStatus.Pending)
            throw new InvalidOperationException($"工作项当前状态不允许开始处理（当前状态: {entity.FStatus}）");

        var fromStatus = entity.FStatus;
        entity.FStatus = (int)WorkItemStatus.InProgress;
        entity.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        await WriteLogAsync(workItemId, operatorId, "Started", fromStatus, entity.FStatus, "开始处理");

        return MapToDto(entity);
    }

    public async Task<WorkItemDto> CompleteAsync(long workItemId, long operatorId, string? result = null, string? remark = null, bool enforceAssignee = true)
    {
        var entity = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId)
            ?? throw new InvalidOperationException($"工作项 {workItemId} 不存在");

        EnsureAssignee(entity, operatorId, enforceAssignee);

        if (entity.FStatus == (int)WorkItemStatus.Completed)
            throw new InvalidOperationException("工作项已完成，不可重复完成");

        var fromStatus = entity.FStatus;
        entity.FStatus = (int)WorkItemStatus.Completed;
        entity.FCompletedTime = DateTime.Now;
        entity.FUpdateTime = DateTime.Now;
        entity.FResult = result;
        entity.FRemark = remark;

        await _db.SaveChangesAsync();
        await WriteLogAsync(workItemId, operatorId, "Completed", fromStatus, entity.FStatus, remark ?? "完成处理");

        // 触发完成回调
        try
        {
            await _callback.OnCompletedAsync(workItemId, result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工作项 {WorkItemId} 完成回调执行失败", workItemId);
        }

        return MapToDto(entity);
    }

    public async Task<WorkItemDto> CancelAsync(long workItemId, long operatorId, string? reason = null, bool enforceAssignee = true)
    {
        var entity = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId)
            ?? throw new InvalidOperationException($"工作项 {workItemId} 不存在");

        EnsureAssignee(entity, operatorId, enforceAssignee);

        if (entity.FStatus == (int)WorkItemStatus.Completed)
            throw new InvalidOperationException("工作项已完成，不可取消");

        var fromStatus = entity.FStatus;
        entity.FStatus = (int)WorkItemStatus.Cancelled;
        entity.FUpdateTime = DateTime.Now;
        entity.FRemark = reason;

        await _db.SaveChangesAsync();
        await WriteLogAsync(workItemId, operatorId, "Cancelled", fromStatus, entity.FStatus, reason ?? "取消工作项");

        // 触发取消回调
        try
        {
            await _callback.OnCancelledAsync(workItemId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工作项 {WorkItemId} 取消回调执行失败", workItemId);
        }

        return MapToDto(entity);
    }

    public async Task<WorkItemStatsDto> GetStatsAsync(long userId)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var now = DateTime.Now;

        var stats = new WorkItemStatsDto
        {
            PendingCount = await _db.Set<WfWorkItem>()
                .CountAsync(w => w.FAssigneeId == userId && w.FStatus == (int)WorkItemStatus.Pending),
            InProgressCount = await _db.Set<WfWorkItem>()
                .CountAsync(w => w.FAssigneeId == userId && w.FStatus == (int)WorkItemStatus.InProgress),
            CompletedTodayCount = await _db.Set<WfWorkItem>()
                .CountAsync(w => w.FAssigneeId == userId && w.FStatus == (int)WorkItemStatus.Completed
                    && w.FCompletedTime >= today && w.FCompletedTime < tomorrow),
            OverdueCount = await _db.Set<WfWorkItem>()
                .CountAsync(w => w.FAssigneeId == userId
                    && w.FStatus != (int)WorkItemStatus.Completed
                    && w.FStatus != (int)WorkItemStatus.Cancelled
                    && w.FDeadline.HasValue && w.FDeadline < now)
        };

        return stats;
    }

    /// <summary>
    /// 对象级授权：用户发起的开始/完成/取消必须由该工作项的指派人本人操作，
    /// 防止任意登录用户通过遍历 FID 操作他人工作项（IDOR）。
    /// 系统/编排内部流程传 enforceAssignee=false 放行。
    /// </summary>
    private static void EnsureAssignee(WfWorkItem entity, long operatorId, bool enforceAssignee)
    {
        if (!enforceAssignee)
            return;

        if (entity.FAssigneeId != operatorId)
            throw new UnauthorizedAccessException("无权操作该工作项：仅指派人本人可处理");
    }

    #region 生命周期管理

    public async Task ClaimAsync(long workItemId, long userId)
    {
        var affected = await _db.Database.ExecuteSqlRawAsync(
            @"UPDATE [WF工作项] SET [F处理人ID] = {0}, [F认领时间] = {1}, [F状态] = 1 
              WHERE [FID] = {2} AND [F处理人ID] IS NULL AND [F状态] = 0",
            userId, DateTime.Now, workItemId);

        if (affected == 0)
            throw new InvalidOperationException("该工作项已被认领或状态不允许认领");

        // 推送通知
        try
        {
            await _workHubNotifier.NotifyWorkItemStatusChangedAsync(userId, new { WorkItemId = workItemId, Status = 1, Action = "Claimed" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工作项 {WorkItemId} 认领通知发送失败", workItemId);
        }

        _logger.LogInformation("工作项 {WorkItemId} 已被用户 {UserId} 认领", workItemId, userId);
    }

    public async Task DismissAsync(long workItemId, long userId)
    {
        var entity = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId)
            ?? throw new InvalidOperationException($"工作项 {workItemId} 不存在");

        if (entity.FStatus == (int)WorkItemStatus.Completed || entity.FStatus == (int)WorkItemStatus.Cancelled)
            throw new InvalidOperationException("工作项已结束，不可关闭");

        var fromStatus = entity.FStatus;
        entity.FStatus = (int)WorkItemStatus.Dismissed;
        entity.FResolvedAt = DateTime.Now;
        entity.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        await WriteLogAsync(workItemId, userId, "Dismissed", fromStatus, entity.FStatus, "WorkHub手动关闭");

        // 发布状态变更事件
        await PublishStatusChangedEventAsync(entity);
    }

    public async Task ResolveAsync(long workItemId)
    {
        var entity = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId)
            ?? throw new InvalidOperationException($"工作项 {workItemId} 不存在");

        if (entity.FStatus == (int)WorkItemStatus.Completed || entity.FStatus == (int)WorkItemStatus.Cancelled)
            return; // 已结束的不重复操作

        var fromStatus = entity.FStatus;
        entity.FStatus = (int)WorkItemStatus.Completed;
        entity.FCompletedTime = DateTime.Now;
        entity.FResolvedAt = DateTime.Now;
        entity.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        await WriteLogAsync(workItemId, 0, "Resolved", fromStatus, entity.FStatus, "质量中心解决关闭");

        // 发布状态变更事件
        await PublishStatusChangedEventAsync(entity);
    }

    public async Task CancelBySystemAsync(long workItemId)
    {
        var entity = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId)
            ?? throw new InvalidOperationException($"工作项 {workItemId} 不存在");

        if (entity.FStatus == (int)WorkItemStatus.Completed || entity.FStatus == (int)WorkItemStatus.Cancelled)
            return; // 已结束的不重复操作

        var fromStatus = entity.FStatus;
        entity.FStatus = (int)WorkItemStatus.Cancelled;
        entity.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        await WriteLogAsync(workItemId, 0, "CancelledBySystem", fromStatus, entity.FStatus, "系统自动取消（重新计费）");

        // 发布状态变更事件
        await PublishStatusChangedEventAsync(entity);
    }

    public async Task<bool> HasActiveWorkItemAsync(long bizId, string bizType)
    {
        return await _db.Set<WfWorkItem>()
            .AnyAsync(w => w.FBizId == bizId
                && w.FBizType == bizType
                && w.FStatus != (int)WorkItemStatus.Completed
                && w.FStatus != (int)WorkItemStatus.Cancelled
                && w.FStatus != (int)WorkItemStatus.Dismissed);
    }

    public async Task<List<long>> GetActiveWorkItemIdsAsync(long bizId, string bizType)
    {
        return await _db.Set<WfWorkItem>()
            .Where(w => w.FBizId == bizId
                && w.FBizType == bizType
                && w.FStatus != (int)WorkItemStatus.Completed
                && w.FStatus != (int)WorkItemStatus.Cancelled
                && w.FStatus != (int)WorkItemStatus.Dismissed)
            .Select(w => w.FID)
            .ToListAsync();
    }

    public async Task SetDispatchWarningAsync(long workItemId, string warning)
    {
        var entity = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId)
            ?? throw new InvalidOperationException($"工作项 {workItemId} 不存在");

        entity.FDispatchWarning = warning;
        entity.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    private async Task PublishStatusChangedEventAsync(WfWorkItem entity)
    {
        try
        {
            await _eventDispatcher.PublishAsync(new WorkItemStatusChangedEvent
            {
                WorkItemId = entity.FID,
                NewStatus = entity.FStatus,
                BizType = entity.FBizType ?? string.Empty,
                BizId = entity.FBizId ?? 0,
                Category = entity.FCategory,
                ModuleCode = "Workflow"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工作项 {WorkItemId} 状态变更事件发布失败", entity.FID);
        }
    }

    #endregion

    #region Private Helpers

    private async Task WriteLogAsync(long workItemId, long operatorId, string action, int? fromStatus, int? toStatus, string? content)
    {
        var log = new WfWorkItemLog
        {
            FWorkItemId = workItemId,
            FOperatorId = operatorId,
            FAction = action,
            FFromStatus = fromStatus,
            FToStatus = toStatus,
            FContent = content
        };
        _db.Set<WfWorkItemLog>().Add(log);
        await _db.SaveChangesAsync();
    }

    private static WorkItemDto MapToDto(WfWorkItem entity)
    {
        return new WorkItemDto
        {
            Id = entity.FID,
            Uid = entity.FUID,
            Title = entity.FTitle,
            Description = entity.FDescription,
            Type = entity.FType,
            Source = entity.FSource,
            Status = entity.FStatus,
            Priority = entity.FPriority,
            ChainId = entity.FChainId,
            ChainSeq = entity.FChainSeq,
            DataScopeId = entity.FDataScopeId,
            CreatorId = entity.FCreatorId,
            AssigneeId = entity.FAssigneeId,
            AssigneeName = entity.FAssigneeName,
            Module = entity.FModule,
            BizType = entity.FBizType,
            BizId = entity.FBizId,
            DetailRoute = entity.FDetailRoute,
            CreateTime = entity.FCreateTime,
            Deadline = entity.FDeadline,
            CompletedTime = entity.FCompletedTime,
            Result = entity.FResult,
            Remark = entity.FRemark
        };
    }

    #endregion
}
