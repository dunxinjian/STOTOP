using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>
/// 流程定义编辑锁（M7-1 / 设计 E7）。单写者并发锁 + 接管协议。
/// 心跳 30s、超时 120s 释放、接管等待 60s。
/// 门禁：仅 [Authorize]——与所保护的编辑端点（Update/Publish/SaveDraftVersion 均只挂 [Authorize]）一致；
/// 若加更严的 [RequirePermission] 反而会把能编辑（[Authorize]）却无该权限码的用户挡在锁外，令锁对其失效。
/// </summary>
[Authorize]
[ApiController]
[Route("api/cardflow/definitions")]
public class FlowDefinitionLockController : ControllerBase
{
    private readonly IDefinitionEditLockService _lockService;

    public FlowDefinitionLockController(IDefinitionEditLockService lockService)
    {
        _lockService = lockService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private string GetUserName() => User.FindFirst("userName")?.Value ?? User.Identity?.Name ?? GetUserId().ToString();

    /// <summary>获取编辑锁（进入编辑页时调用）</summary>
    [HttpPost("{id}/lock/acquire")]
    public async Task<ApiResult<LockStateDto>> Acquire(long id)
    {
        var result = await _lockService.AcquireAsync(id, GetUserId(), GetUserName());
        return ApiResult<LockStateDto>.Success(result);
    }

    /// <summary>持锁端心跳续期（30s 一次）</summary>
    [HttpPost("{id}/lock/heartbeat")]
    public async Task<ApiResult<LockStateDto>> Heartbeat(long id)
    {
        var result = await _lockService.HeartbeatAsync(id, GetUserId());
        return ApiResult<LockStateDto>.Success(result);
    }

    /// <summary>只读端申请接管（全局唯一，成功后经 SignalR 通知持锁端）</summary>
    [HttpPost("{id}/lock/takeover-request")]
    public async Task<ApiResult<LockStateDto>> RequestTakeover(long id)
    {
        try
        {
            var result = await _lockService.RequestTakeoverAsync(id, GetUserId(), GetUserName());
            return ApiResult<LockStateDto>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<LockStateDto>.Fail(ex.Message);
        }
    }

    /// <summary>持锁端响应接管请求（accept=true 原子移交 / false 拒绝）</summary>
    [HttpPost("{id}/lock/takeover-respond")]
    public async Task<ApiResult<LockStateDto>> RespondTakeover(long id, [FromBody] TakeoverRespondRequest request)
    {
        var result = await _lockService.RespondTakeoverAsync(id, GetUserId(), request.Accept);
        return ApiResult<LockStateDto>.Success(result);
    }
}
