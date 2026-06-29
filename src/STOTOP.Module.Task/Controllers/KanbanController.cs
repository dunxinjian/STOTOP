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
[Route("api/task/kanban")]
public class KanbanController : ControllerBase
{
    private readonly IKanbanService _kanbanService;

    public KanbanController(IKanbanService kanbanService)
    {
        _kanbanService = kanbanService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
    private bool IsAdmin() => User.IsInRole("admin");

    /// <summary>获取看板数据（按状态分组）</summary>
    [HttpGet]
    [RequirePermission(TaskPermissions.KanbanView)]
    public async global::System.Threading.Tasks.Task<ApiResult<KanbanDataDto>> GetKanbanData([FromQuery] KanbanQueryRequest query)
    {
        return await _kanbanService.GetKanbanDataAsync(query, GetOrgId(), GetUserId(), IsAdmin());
    }

    /// <summary>拖拽移动（变更状态+排序）</summary>
    [HttpPut("move")]
    [RequirePermission(TaskPermissions.KanbanView)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Move([FromBody] KanbanMoveRequest request)
    {
        return await _kanbanService.MoveAsync(request);
    }
}
