using STOTOP.Core.Models;

namespace STOTOP.Core.Interfaces;

/// <summary>
/// 对话消息推送接口 - 通过 SignalR 等机制向用户推送对话消息/卡片
/// </summary>
public interface IConversationNotifier
{
    /// <summary>
    /// 向指定会话推送消息
    /// </summary>
    /// <param name="sessionCode">会话编号</param>
    /// <param name="message">消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PushMessageAsync(string sessionCode, ConversationMessageInfo message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向指定会话推送卡片
    /// </summary>
    /// <param name="sessionCode">会话编号</param>
    /// <param name="card">卡片数据</param>
    /// <param name="relatedTaskId">关联任务ID（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PushCardAsync(string sessionCode, CardPayload card, long? relatedTaskId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向指定用户的所有会话推送通知
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="message">消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyUserAsync(long userId, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通知卡片状态已更新
    /// </summary>
    /// <param name="sessionCode">会话编号</param>
    /// <param name="messageId">消息ID</param>
    /// <param name="status">新状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyCardUpdatedAsync(string sessionCode, long messageId, int status, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通知会话已完成
    /// </summary>
    /// <param name="sessionCode">会话编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifySessionCompletedAsync(string sessionCode, CancellationToken cancellationToken = default);
}
