using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.WebAPI.Hubs;

namespace STOTOP.WebAPI.Services;

/// <summary>
/// WorkHub 实时通知实现 - 通过 SignalR 推送到前端
/// </summary>
public class WorkHubNotifier : IWorkHubNotifier
{
    private readonly IHubContext<WorkHubHub> _hubContext;
    private readonly ILogger<WorkHubNotifier> _logger;

    public WorkHubNotifier(IHubContext<WorkHubHub> hubContext, ILogger<WorkHubNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// 通知指定用户刷新统计 → 客户端事件: StatsUpdated
    /// </summary>
    public async Task RefreshStatsAsync(long userId)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("StatsUpdated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RefreshStatsAsync 推送失败: userId={UserId}", userId);
        }
    }

    /// <summary>
    /// 推送新工作项 → 客户端事件: WorkItemAdded
    /// </summary>
    public async Task AddWorkItemAsync(long userId, WorkItemDto item)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("WorkItemAdded", item);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AddWorkItemAsync 推送失败: userId={UserId}, itemId={ItemId}", userId, item.Id);
        }
    }

    /// <summary>
    /// 通知移除工作项 → 客户端事件: WorkItemRemoved
    /// </summary>
    public async Task RemoveWorkItemAsync(long userId, long itemId, string source)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("WorkItemRemoved", new { itemId, source });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RemoveWorkItemAsync 推送失败: userId={UserId}, itemId={ItemId}", userId, itemId);
        }
    }

    /// <summary>
    /// 推送工作项更新 → 客户端事件: WorkItemUpdated
    /// </summary>
    public async Task UpdateWorkItemAsync(long userId, WorkItemDto item)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("WorkItemUpdated", item);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UpdateWorkItemAsync 推送失败: userId={UserId}, itemId={ItemId}", userId, item.Id);
        }
    }

    /// <summary>
    /// 推送链路评论 → 客户端事件: ChainCommentAdded
    /// </summary>
    public async Task NotifyChainCommentAsync(string chainId, object comment)
    {
        try
        {
            await _hubContext.Clients
                .Group($"chain_{chainId}")
                .SendAsync("ChainCommentAdded", comment);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NotifyChainCommentAsync 推送失败: chainId={ChainId}", chainId);
        }
    }

    /// <summary>
    /// 通知用户工作项状态变更 → 客户端事件: WorkItemStatusChanged
    /// </summary>
    public async Task NotifyWorkItemStatusChangedAsync(long userId, object item)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("WorkItemStatusChanged", item);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NotifyWorkItemStatusChangedAsync 推送失败: userId={UserId}", userId);
        }
    }
}
