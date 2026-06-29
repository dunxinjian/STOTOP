using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/notifications")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationController(INotificationService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>获取通知列表（分页，支持筛选已读/未读）</summary>
    [HttpGet]
    [RequirePermission(TaskPermissions.NotificationView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<NotificationListDto>>> GetPagedList([FromQuery] NotificationPagedRequest query)
    {
        return await _service.GetPagedListAsync(query, GetUserId());
    }

    /// <summary>获取未读通知数</summary>
    [HttpGet("unread-count")]
    [RequirePermission(TaskPermissions.NotificationView)]
    public async global::System.Threading.Tasks.Task<ApiResult<UnreadCountDto>> GetUnreadCount()
    {
        return await _service.GetUnreadCountAsync(GetUserId());
    }

    /// <summary>标记已读</summary>
    [HttpPut("{id}/read")]
    [RequirePermission(TaskPermissions.NotificationView)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> MarkRead(long id)
    {
        return await _service.MarkReadAsync(id, GetUserId());
    }

    /// <summary>全部标记已读</summary>
    [HttpPut("read-all")]
    [RequirePermission(TaskPermissions.NotificationView)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> MarkAllRead()
    {
        return await _service.MarkAllReadAsync(GetUserId());
    }
}
