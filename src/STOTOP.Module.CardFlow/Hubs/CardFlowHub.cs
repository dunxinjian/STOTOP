using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace STOTOP.Module.CardFlow.Hubs;

[Authorize]
public class CardFlowHub : Hub
{
    // 订阅卡片状态变更
    public Task SubscribeCard(long cardId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"card-{cardId}");

    public Task UnsubscribeCard(long cardId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"card-{cardId}");

    // 订阅用户待办通知
    public Task SubscribeUserTodo(long userId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"todo-{userId}");

    public Task UnsubscribeUserTodo(long userId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"todo-{userId}");

    // 订阅流程监控（管理员）
    public Task SubscribeMonitor()
        => Groups.AddToGroupAsync(Context.ConnectionId, "cardflow-monitor");

    public Task UnsubscribeMonitor()
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, "cardflow-monitor");

    // ===== Task 3 迁移：原 DataCenter ProgressHub 订阅能力（D5）=====
    // 订阅导入批次进度（迁自 ProgressHub.SubscribeImportBatch）
    public Task SubscribeBatch(long batchId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"batch-{batchId}");

    public Task UnsubscribeBatch(long batchId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"batch-{batchId}");

    // 订阅下载任务进度（迁自 ProgressHub.SubscribeDownloadTask）
    public Task SubscribeDownloadTask(long taskId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"download-{taskId}");

    public Task UnsubscribeDownloadTask(long taskId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"download-{taskId}");

    // 订阅处理状态全局推送（迁自 ProgressHub.SubscribeProcessing）
    public Task SubscribeProcessing()
        => Groups.AddToGroupAsync(Context.ConnectionId, "processing");

    public Task UnsubscribeProcessing()
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, "processing");

    // 订阅首页统计（迁自 ProgressHub.SubscribeHomeStats）
    public Task SubscribeHomeStats()
        => Groups.AddToGroupAsync(Context.ConnectionId, "home-stats");

    public Task UnsubscribeHomeStats()
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, "home-stats");
}
