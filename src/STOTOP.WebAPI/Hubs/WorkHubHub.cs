using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace STOTOP.WebAPI.Hubs;

/// <summary>
/// WorkHub SignalR Hub
/// 
/// 服务端方法（客户端可调用）:
///   - JoinUserChannel(userId)   : 加入个人频道
///   - LeaveUserChannel(userId)  : 离开个人频道
///
/// 客户端事件（服务端推送，由 IWorkHubNotifier 通过 IHubContext 触发）:
///   - StatsUpdated              : 通知客户端重新拉取统计数据
///   - WorkItemAdded(item)       : 推送新工作项 (WorkItemDto)
///   - WorkItemRemoved(payload)  : 通知移除工作项 { itemId, source }
///   - WorkItemUpdated(item)     : 推送工作项更新 (WorkItemDto)
/// </summary>
[Authorize]
public class WorkHubHub : Hub
{
    /// <summary>
    /// 用户加入个人频道（以 userId 为 Group）
    /// </summary>
    public async Task JoinUserChannel(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
    }

    /// <summary>
    /// 用户离开个人频道
    /// </summary>
    public async Task LeaveUserChannel(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
    }

    /// <summary>加入链路频道（接收链路相关实时推送）</summary>
    public async Task JoinChainChannel(string chainId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chain_{chainId}");
    }

    /// <summary>离开链路频道</summary>
    public async Task LeaveChainChannel(string chainId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chain_{chainId}");
    }
}
