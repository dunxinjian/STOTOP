using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/kanban")]
public class KanbanController : ControllerBase
{
    private readonly IKanbanService _kanbanService;
    private readonly IAdminAuthorizationService _adminAuth;

    public KanbanController(IKanbanService kanbanService, IAdminAuthorizationService adminAuth)
    {
        _kanbanService = kanbanService;
        _adminAuth = adminAuth;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
    // 口径统一：走中心 IAdminAuthorizationService（认 OA_ADMIN 角色 Claim），不再按 IsInRole("admin") 角色名字面量判定。
    private bool IsAdmin() => _adminAuth.IsAdmin(User);

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
