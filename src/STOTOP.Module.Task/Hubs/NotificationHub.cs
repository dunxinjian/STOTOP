using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace STOTOP.Module.Task.Hubs;

/// <summary>
/// 通知实时推送 Hub，用于 Task 模块业务通知的 SignalR 推送。
/// 客户端事件: ReceiveNotification - 接收单条通知
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
}
