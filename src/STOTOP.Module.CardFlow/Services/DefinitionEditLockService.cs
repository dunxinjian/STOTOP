using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Hubs;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 流程定义编辑锁服务（M7-1 / 设计 E7）。
/// 单写者并发锁 + 接管协议。核心不变量：
/// - 唯一写者：任意时刻至多一人持写锁。
/// - 强制 flush 在弹窗时（经 SignalR 推送 takeoverRequested 给持锁端）。
/// - 接管请求全局唯一（无排队队列）。
/// - 离线兜底：心跳超时后直接移交。
/// </summary>
public sealed class DefinitionEditLockService : IDefinitionEditLockService
{
    /// <summary>心跳超时秒数：超过此值未续期视为死锁</summary>
    public const int HeartbeatTimeoutSeconds = 120;
    /// <summary>接管等待超时秒数：超过此值未响应视为同意移交</summary>
    public const int TakeoverTimeoutSeconds = 60;

    private readonly STOTOPDbContext _dbContext;
    private readonly IHubContext<CardFlowHub>? _hubContext;

    public DefinitionEditLockService(STOTOPDbContext dbContext, IHubContext<CardFlowHub>? hubContext = null)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
    }

    public async Task<LockStateDto> AcquireAsync(long definitionId, long userId, string userName)
    {
        var lockRow = await GetLockRowTracking(definitionId);

        if (lockRow == null)
        {
            // 无锁 → 创建并持有
            lockRow = new CfDefinitionEditLock
            {
                FFlowDefinitionId = definitionId,
                FHolderId = userId,
                FHolderName = userName,
                FAcquiredTime = DateTime.Now,
                FHeartbeatAt = DateTime.Now,
            };
            // FOrgId / FTenantId 由 DbContext FillOrgId 自动填充
            _dbContext.Set<CfDefinitionEditLock>().Add(lockRow);
            await _dbContext.SaveChangesAsync();
            return BuildDto(lockRow, userId);
        }

        // 已有锁：判断是否死锁（超时）
        if (IsStale(lockRow))
        {
            // 死锁 → 抢占
            lockRow.FHolderId = userId;
            lockRow.FHolderName = userName;
            lockRow.FAcquiredTime = DateTime.Now;
            lockRow.FHeartbeatAt = DateTime.Now;
            ClearTakeoverRequest(lockRow);
            await _dbContext.SaveChangesAsync();
            return BuildDto(lockRow, userId);
        }

        // 活锁：是自己 → 续期返回；是他人 → 返回只读信息
        if (lockRow.FHolderId == userId)
        {
            lockRow.FHeartbeatAt = DateTime.Now;
            await _dbContext.SaveChangesAsync();
        }

        return BuildDto(lockRow, userId);
    }

    public async Task<LockStateDto> HeartbeatAsync(long definitionId, long userId)
    {
        var lockRow = await GetLockRowTracking(definitionId);
        if (lockRow == null)
            return new LockStateDto { Held = false };

        // 非 holder → 告知已丢锁
        if (lockRow.FHolderId != userId)
            return BuildDto(lockRow, userId);

        // 检查是否有超时的接管请求 → 自动移交
        if (HasPendingTakeover(lockRow) && IsTakeoverExpired(lockRow))
        {
            await ExecuteTransferAsync(lockRow, lockRow.FTakeoverRequesterId!.Value,
                lockRow.FTakeoverRequesterName ?? "");
            return BuildDto(lockRow, userId);
        }

        // 正常续期
        lockRow.FHeartbeatAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();
        return BuildDto(lockRow, userId);
    }

    public async Task<LockStateDto> RequestTakeoverAsync(long definitionId, long requesterId, string requesterName)
    {
        var lockRow = await GetLockRowTracking(definitionId);
        if (lockRow == null)
            return new LockStateDto { Held = false };

        // 锁已死 → 申请人直接 acquire
        if (IsStale(lockRow))
        {
            lockRow.FHolderId = requesterId;
            lockRow.FHolderName = requesterName;
            lockRow.FAcquiredTime = DateTime.Now;
            lockRow.FHeartbeatAt = DateTime.Now;
            ClearTakeoverRequest(lockRow);
            await _dbContext.SaveChangesAsync();
            return BuildDto(lockRow, requesterId);
        }

        // 自己持锁 → 无需接管
        if (lockRow.FHolderId == requesterId)
            return BuildDto(lockRow, requesterId);

        // 全局唯一：已有未过期接管请求且非本人
        if (HasPendingTakeover(lockRow) && !IsTakeoverExpired(lockRow)
            && lockRow.FTakeoverRequesterId != requesterId)
        {
            throw new InvalidOperationException(
                $"{lockRow.FTakeoverRequesterName}已有接管请求处理中，请稍后重试");
        }

        // 登记接管请求
        lockRow.FTakeoverRequesterId = requesterId;
        lockRow.FTakeoverRequesterName = requesterName;
        lockRow.FTakeoverRequestedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        // 经 SignalR 通知持锁端
        await NotifyTakeoverRequestedAsync(definitionId, requesterId, requesterName);

        return BuildDto(lockRow, requesterId);
    }

    public async Task<LockStateDto> RespondTakeoverAsync(long definitionId, long holderId, bool accept)
    {
        var lockRow = await GetLockRowTracking(definitionId);
        if (lockRow == null)
            return new LockStateDto { Held = false };

        // 非 holder 不能响应
        if (lockRow.FHolderId != holderId)
            return BuildDto(lockRow, holderId);

        // 无待响应请求
        if (!HasPendingTakeover(lockRow))
            return BuildDto(lockRow, holderId);

        if (accept)
        {
            var newHolderId = lockRow.FTakeoverRequesterId!.Value;
            var newHolderName = lockRow.FTakeoverRequesterName ?? "";
            await ExecuteTransferAsync(lockRow, newHolderId, newHolderName);
            return BuildDto(lockRow, holderId);
        }
        else
        {
            // 拒绝：清请求 + 推 takeoverRejected
            ClearTakeoverRequest(lockRow);
            await _dbContext.SaveChangesAsync();
            await NotifyTakeoverRejectedAsync(definitionId);
            return BuildDto(lockRow, holderId);
        }
    }

    // ═══════════ 内部方法 ═══════════

    private async Task<CfDefinitionEditLock?> GetLockRowTracking(long definitionId)
    {
        return await _dbContext.Set<CfDefinitionEditLock>()
            .AsTracking()
            .FirstOrDefaultAsync(l => l.FFlowDefinitionId == definitionId);
    }

    private static bool IsStale(CfDefinitionEditLock lockRow)
        => (DateTime.Now - lockRow.FHeartbeatAt).TotalSeconds > HeartbeatTimeoutSeconds;

    private static bool HasPendingTakeover(CfDefinitionEditLock lockRow)
        => lockRow.FTakeoverRequesterId.HasValue;

    private static bool IsTakeoverExpired(CfDefinitionEditLock lockRow)
        => lockRow.FTakeoverRequestedAt.HasValue
           && (DateTime.Now - lockRow.FTakeoverRequestedAt.Value).TotalSeconds > TakeoverTimeoutSeconds;

    private static void ClearTakeoverRequest(CfDefinitionEditLock lockRow)
    {
        lockRow.FTakeoverRequesterId = null;
        lockRow.FTakeoverRequesterName = null;
        lockRow.FTakeoverRequestedAt = null;
    }

    /// <summary>原子移交：holder 改为新人、清请求段、刷新时间戳、推事件。单次 SaveChanges 保证唯一写者不变量。</summary>
    private async Task ExecuteTransferAsync(CfDefinitionEditLock lockRow, long newHolderId, string newHolderName)
    {
        var definitionId = lockRow.FFlowDefinitionId;
        lockRow.FHolderId = newHolderId;
        lockRow.FHolderName = newHolderName;
        lockRow.FAcquiredTime = DateTime.Now;
        lockRow.FHeartbeatAt = DateTime.Now;
        ClearTakeoverRequest(lockRow);
        await _dbContext.SaveChangesAsync();

        await NotifyTakeoverGrantedAsync(definitionId, newHolderId, newHolderName);
    }

    private static LockStateDto BuildDto(CfDefinitionEditLock lockRow, long currentUserId)
    {
        var dto = new LockStateDto
        {
            Held = true,
            HolderId = lockRow.FHolderId,
            HolderName = lockRow.FHolderName,
            IsSelf = lockRow.FHolderId == currentUserId,
            AcquiredAt = lockRow.FAcquiredTime,
            HeartbeatAt = lockRow.FHeartbeatAt,
        };
        if (HasPendingTakeover(lockRow))
        {
            dto.Takeover = new LockTakeoverDto
            {
                RequesterId = lockRow.FTakeoverRequesterId!.Value,
                RequesterName = lockRow.FTakeoverRequesterName ?? "",
                RequestedAt = lockRow.FTakeoverRequestedAt!.Value,
            };
        }
        return dto;
    }

    // ═══════════ SignalR 推送 ═══════════

    private async Task NotifyTakeoverRequestedAsync(long definitionId, long requesterId, string requesterName)
    {
        if (_hubContext == null) return;
        await _hubContext.Clients.Group($"flowdef-{definitionId}")
            .SendAsync("takeoverRequested", new { requesterId, requesterName });
    }

    private async Task NotifyTakeoverGrantedAsync(long definitionId, long newHolderId, string newHolderName)
    {
        if (_hubContext == null) return;
        await _hubContext.Clients.Group($"flowdef-{definitionId}")
            .SendAsync("takeoverGranted", new { newHolderId, newHolderName });
    }

    private async Task NotifyTakeoverRejectedAsync(long definitionId)
    {
        if (_hubContext == null) return;
        await _hubContext.Clients.Group($"flowdef-{definitionId}")
            .SendAsync("takeoverRejected", new { });
    }
}
