namespace STOTOP.Module.Task.Services.DingTalk;

/// <summary>
/// 钉钉消息推送服务接口
/// </summary>
public interface IDingTalkMessageService
{
    /// <summary>
    /// 推送评论到钉钉
    /// </summary>
    global::System.Threading.Tasks.Task PushCommentAsync(long commentId);

    /// <summary>
    /// 推送进度上报到钉钉
    /// </summary>
    global::System.Threading.Tasks.Task PushProgressAsync(long progressId);

    /// <summary>
    /// 推送状态变更
    /// </summary>
    global::System.Threading.Tasks.Task PushStatusChangeAsync(long taskId);

    /// <summary>
    /// 处理待推送消息（供 Job 调用）
    /// </summary>
    global::System.Threading.Tasks.Task ProcessPendingMessagesAsync();
}
