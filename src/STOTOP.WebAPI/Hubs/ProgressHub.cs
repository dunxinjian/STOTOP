using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace STOTOP.WebAPI.Hubs;

[Authorize]
public class ProgressHub : Hub
{
    public Task SubscribeImportBatch(long batchId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"import-{batchId}");

    public Task UnsubscribeImportBatch(long batchId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"import-{batchId}");

    public Task SubscribeDownloadTask(long taskId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"download-{taskId}");

    public Task UnsubscribeDownloadTask(long taskId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"download-{taskId}");

    public Task SubscribeProcessing()
        => Groups.AddToGroupAsync(Context.ConnectionId, "processing");

    public Task UnsubscribeProcessing()
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, "processing");

    public Task SubscribeHomeStats()
        => Groups.AddToGroupAsync(Context.ConnectionId, "home-stats");

    public Task UnsubscribeHomeStats()
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, "home-stats");

    public async Task SubscribeDingTalkSync()
        => await Groups.AddToGroupAsync(Context.ConnectionId, "dingtalk-sync");

    public async Task UnsubscribeDingTalkSync()
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, "dingtalk-sync");
}
