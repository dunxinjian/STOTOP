using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Controllers;

[Authorize]
[ApiController]
[Route("api/workflow/trigger-actions")]
public class TriggerActionController : ControllerBase
{
    private readonly ITriggerActionService _triggerActionService;

    public TriggerActionController(ITriggerActionService triggerActionService)
    {
        _triggerActionService = triggerActionService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>获取当前用户可用的触发动作列表</summary>
    [HttpGet("available")]
    public async Task<ApiResult<List<TriggerActionDto>>> GetAvailable()
    {
        var userId = GetUserId();
        var orgId = GetOrgId();
        var result = await _triggerActionService.GetAvailableActionsAsync(userId, orgId);
        return ApiResult<List<TriggerActionDto>>.Success(result);
    }

    /// <summary>获取所有动作（管理用）</summary>
    [HttpGet]
    public async Task<ApiResult<List<TriggerActionDto>>> GetAll()
    {
        var result = await _triggerActionService.GetAllActionsAsync();
        return ApiResult<List<TriggerActionDto>>.Success(result);
    }

    /// <summary>切换启用状态</summary>
    [HttpPost("{id}/toggle")]
    public async Task<ApiResult> Toggle(long id)
    {
        try
        {
            await _triggerActionService.ToggleAsync(id);
            return ApiResult.Ok("切换成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
