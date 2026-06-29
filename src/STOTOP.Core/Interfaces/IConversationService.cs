namespace STOTOP.Core.Interfaces;

/// <summary>
/// 对话会话管理接口 - 通用的对话式交互核心服务
/// </summary>
public interface IConversationService
{
    #region 会话管理

    /// <summary>
    /// 创建新的对话会话
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="orgId">组织ID</param>
    /// <param name="sessionType">会话类型（可选，默认为发起流程）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话编号</returns>
    Task<string> CreateSessionAsync(long userId, long orgId, int sessionType = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据会话编号获取会话详情
    /// </summary>
    /// <param name="sessionCode">会话编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话信息，不存在时返回 null</returns>
    Task<ConversationSessionInfo?> GetSessionByCodeAsync(string sessionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的对话会话列表（分页）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话列表</returns>
    Task<List<ConversationSessionInfo>> GetUserSessionsAsync(long userId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取会话的消息列表（分页）
    /// </summary>
    /// <param name="sessionCode">会话编号</param>
    /// <param name="skip">跳过条数</param>
    /// <param name="take">获取条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表</returns>
    Task<List<ConversationMessageInfo>> GetMessagesAsync(string sessionCode, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// 关闭会话
    /// </summary>
    /// <param name="sessionCode">会话编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CloseSessionAsync(string sessionCode, CancellationToken cancellationToken = default);

    #endregion

    #region 消息处理

    /// <summary>
    /// 推送系统文本消息
    /// </summary>
    /// <param name="sessionCode">会话编号</param>
    /// <param name="text">消息文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息ID</returns>
    Task<long> PushSystemMessageAsync(string sessionCode, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// 推送卡片消息
    /// </summary>
    /// <param name="sessionCode">会话编号</param>
    /// <param name="card">卡片数据</param>
    /// <param name="relatedTaskId">关联的任务ID（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息ID</returns>
    Task<long> PushCardAsync(string sessionCode, Models.CardPayload card, long? relatedTaskId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新卡片状态
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="status">状态 - 0:待交互 1:已交互 2:已过期</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateCardStatusAsync(long messageId, int status, CancellationToken cancellationToken = default);

    #endregion
}

#region 会话信息DTO

/// <summary>
/// 对话会话信息
/// </summary>
public class ConversationSessionInfo
{
    public long Id { get; set; }
    public string SessionCode { get; set; } = string.Empty;
    public long UserId { get; set; }
    public long? ProcessInstanceId { get; set; }
    public long? ProcessConfigId { get; set; }

    /// <summary>
    /// 会话类型
    /// </summary>
    public int SessionType { get; set; }

    /// <summary>
    /// 状态 - 0:活跃 1:已完成 2:已关闭
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 关联的业务名称（如流程名称）
    /// </summary>
    public string? BusinessName { get; set; }

    public DateTime CreatedTime { get; set; }
    public DateTime? LastActivityTime { get; set; }

    /// <summary>
    /// 未读消息数
    /// </summary>
    public int UnreadCount { get; set; }
}

/// <summary>
/// 对话消息信息
/// </summary>
public class ConversationMessageInfo
{
    public long Id { get; set; }
    public string SessionCode { get; set; } = string.Empty;

    /// <summary>
    /// 发送方 - 0:系统 1:用户
    /// </summary>
    public int Sender { get; set; }

    /// <summary>
    /// 消息类型 - text/card/action/notification
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// 内容 - 文本内容或序列化的卡片JSON
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 卡片类型
    /// </summary>
    public string? CardType { get; set; }

    public long? RelatedTaskId { get; set; }

    /// <summary>
    /// 卡片状态 - 0:待交互 1:已交互 2:已过期
    /// </summary>
    public int? CardStatus { get; set; }

    public DateTime CreatedTime { get; set; }
}

#endregion
