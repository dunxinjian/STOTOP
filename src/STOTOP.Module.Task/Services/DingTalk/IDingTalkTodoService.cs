namespace STOTOP.Module.Task.Services.DingTalk;

/// <summary>
/// 钉钉待办推送服务接口
/// </summary>
public interface IDingTalkTodoService
{
    /// <summary>
    /// 创建待办记录并调用钉钉 API
    /// </summary>
    global::System.Threading.Tasks.Task CreateTodoAsync(long taskId, long userId);

    /// <summary>
    /// 完成待办
    /// </summary>
    global::System.Threading.Tasks.Task CompleteTodoAsync(long taskId, long userId);

    /// <summary>
    /// 取消待办
    /// </summary>
    global::System.Threading.Tasks.Task CancelTodoAsync(long taskId, long userId);

    /// <summary>
    /// 同步待处理的待办（供 Job 调用）
    /// </summary>
    global::System.Threading.Tasks.Task SyncPendingTodosAsync();
}
