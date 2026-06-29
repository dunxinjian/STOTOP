using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Entities;
using STOTOP.Module.Task.Hubs;

namespace STOTOP.Module.Task.Services;

public class NotificationService : INotificationService
{
    private readonly STOTOPDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(STOTOPDbContext db, IHubContext<NotificationHub> hubContext, ILogger<NotificationService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<ApiResult<PagedResult<NotificationListDto>>> GetPagedListAsync(NotificationPagedRequest query, long receiverId)
    {
        var q = _db.Set<TmNotification>()
            .Where(n => n.FReceiverId == receiverId)
            .AsQueryable();

        // 已读/未读筛选
        if (query.IsRead.HasValue)
        {
            q = q.Where(n => n.FIsRead == query.IsRead.Value);
        }

        // 事件类型筛选
        if (query.EventType.HasValue)
        {
            q = q.Where(n => n.FEventType == query.EventType.Value);
        }

        // 关联类型筛选
        if (query.RelationType.HasValue)
        {
            q = q.Where(n => n.FRelationType == query.RelationType.Value);
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(n => n.FCreateTime)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => new NotificationListDto
            {
                Id = n.FID,
                ReceiverId = n.FReceiverId,
                EventType = n.FEventType,
                Title = n.FTitle,
                Content = n.FContent,
                RelationType = n.FRelationType,
                RelationId = n.FRelationId,
                IsRead = n.FIsRead,
                PushedToDingTalk = n.FPushedToDingTalk,
                CreateTime = n.FCreateTime
            })
            .ToListAsync();

        var result = new PagedResult<NotificationListDto>
        {
            Items = items,
            Total = total,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };

        return ApiResult<PagedResult<NotificationListDto>>.Success(result);
    }

    public async Task<ApiResult<UnreadCountDto>> GetUnreadCountAsync(long receiverId)
    {
        var unreadQuery = _db.Set<TmNotification>()
            .Where(n => n.FReceiverId == receiverId && !n.FIsRead);

        var total = await unreadQuery.CountAsync();

        var byEventType = await unreadQuery
            .GroupBy(n => n.FEventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .ToListAsync();

        var dto = new UnreadCountDto
        {
            Total = total,
            ByEventType = byEventType.ToDictionary(x => x.EventType, x => x.Count)
        };

        return ApiResult<UnreadCountDto>.Success(dto);
    }

    public async Task<ApiResult<bool>> MarkReadAsync(long id, long receiverId)
    {
        var notification = await _db.Set<TmNotification>()
            .FirstOrDefaultAsync(n => n.FID == id && n.FReceiverId == receiverId);

        if (notification == null)
            return ApiResult<bool>.Fail("通知不存在");

        if (!notification.FIsRead)
        {
            notification.FIsRead = true;
            await _db.SaveChangesAsync();
        }

        return ApiResult<bool>.Success(true, "标记已读成功");
    }

    public async Task<ApiResult<bool>> MarkAllReadAsync(long receiverId)
    {
        await _db.Set<TmNotification>()
            .Where(n => n.FReceiverId == receiverId && !n.FIsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.FIsRead, true));

        return ApiResult<bool>.Success(true, "全部标记已读成功");
    }

    public async Task<long> CreateNotificationAsync(long recipientId, int eventType, string title, string content, int relationType, long relationId)
    {
        var notification = new TmNotification
        {
            FReceiverId = recipientId,
            FEventType = eventType,
            FTitle = title,
            FContent = content,
            FRelationType = relationType,
            FRelationId = relationId,
            FIsRead = false,
            FPushedToDingTalk = false,
            FCreateTime = DateTime.Now
        };

        _db.Set<TmNotification>().Add(notification);
        await _db.SaveChangesAsync();

        // SignalR 实时推送通知
        try
        {
            await _hubContext.Clients.User(recipientId.ToString()).SendAsync("ReceiveNotification", new
            {
                id = notification.FID,
                eventType = notification.FEventType,
                title = notification.FTitle,
                content = notification.FContent,
                relationType = notification.FRelationType,
                relationId = notification.FRelationId,
                createTime = notification.FCreateTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR 推送通知失败: UserId={UserId}, NotificationId={NotificationId}", recipientId, notification.FID);
        }

        return notification.FID;
    }

    public async Task<List<long>> CreateBatchNotificationsAsync(List<long> recipientIds, int eventType, string title, string content, int relationType, long relationId)
    {
        var notifications = recipientIds.Select(recipientId => new TmNotification
        {
            FReceiverId = recipientId,
            FEventType = eventType,
            FTitle = title,
            FContent = content,
            FRelationType = relationType,
            FRelationId = relationId,
            FIsRead = false,
            FPushedToDingTalk = false,
            FCreateTime = DateTime.Now
        }).ToList();

        _db.Set<TmNotification>().AddRange(notifications);
        await _db.SaveChangesAsync();

        // SignalR 批量实时推送
        try
        {
            var userGroups = notifications.GroupBy(n => n.FReceiverId);
            foreach (var group in userGroups)
            {
                var userId = group.Key.ToString();
                var payloads = group.Select(n => new
                {
                    id = n.FID,
                    eventType = n.FEventType,
                    title = n.FTitle,
                    content = n.FContent,
                    relationType = n.FRelationType,
                    relationId = n.FRelationId,
                    createTime = n.FCreateTime
                }).ToList();

                foreach (var payload in payloads)
                {
                    await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", payload);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR 批量推送通知失败");
        }

        return notifications.Select(n => n.FID).ToList();
    }
}
