using STOTOP.Core.Models;

namespace STOTOP.Core.Interfaces;

/// <summary>
/// WorkHub 实时通知接口 - 用于通过 SignalR 向前端推送事件
/// </summary>
public interface IWorkHubNotifier
{
    /// <summary>
    /// 通知指定用户刷新 WorkHub 统计
    /// 客户端事件: StatsUpdated
    /// </summary>
    Task RefreshStatsAsync(long userId);

    /// <summary>
    /// 通知指定用户有新的工作项
    /// 客户端事件: WorkItemAdded
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="item">工作项数据</param>
    Task AddWorkItemAsync(long userId, WorkItemDto item);

    /// <summary>
    /// 通知指定用户某个工作项已被移除
    /// 客户端事件: WorkItemRemoved
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="itemId">工作项ID</param>
    /// <param name="source">来源模块</param>
    Task RemoveWorkItemAsync(long userId, long itemId, string source);

    /// <summary>
    /// 通知指定用户某个工作项已更新
    /// 客户端事件: WorkItemUpdated
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="item">更新后的工作项数据</param>
    Task UpdateWorkItemAsync(long userId, WorkItemDto item);

    /// <summary>
    /// 推送链路评论到链路频道
    /// 客户端事件: ChainCommentAdded
    /// </summary>
    Task NotifyChainCommentAsync(string chainId, object comment);

    /// <summary>
    /// 通知用户工作项状态变更
    /// 客户端事件: WorkItemStatusChanged
    /// </summary>
    Task NotifyWorkItemStatusChangedAsync(long userId, object item);
}
