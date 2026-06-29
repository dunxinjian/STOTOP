using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;

namespace STOTOP.Module.Task.Services;

public interface INotificationService
{
    /// <summary>
    /// 获取通知列表（支持已读/未读筛选）
    /// </summary>
    Task<ApiResult<PagedResult<NotificationListDto>>> GetPagedListAsync(NotificationPagedRequest query, long receiverId);

    /// <summary>
    /// 获取未读通知数
    /// </summary>
    Task<ApiResult<UnreadCountDto>> GetUnreadCountAsync(long receiverId);

    /// <summary>
    /// 标记已读
    /// </summary>
    Task<ApiResult<bool>> MarkReadAsync(long id, long receiverId);

    /// <summary>
    /// 全部标记已读
    /// </summary>
    Task<ApiResult<bool>> MarkAllReadAsync(long receiverId);

    /// <summary>
    /// 创建通知（内部方法，供其他服务调用）
    /// </summary>
    Task<long> CreateNotificationAsync(long recipientId, int eventType, string title, string content, int relationType, long relationId);

    /// <summary>
    /// 批量创建通知
    /// </summary>
    Task<List<long>> CreateBatchNotificationsAsync(List<long> recipientIds, int eventType, string title, string content, int relationType, long relationId);
}
