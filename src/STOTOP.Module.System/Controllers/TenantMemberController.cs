using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;
using System.Security.Claims;

namespace STOTOP.Module.System.Controllers;

/// <summary>
/// 租户成员邀请（多租户阶段4D·M8）。加入租户须【邀请确认】：成员发起邀请 → 被邀请人显式接受才生效（堵跨租户身份横向跳板）。
/// </summary>
[Route("api/system/tenant-member")]
[ApiController]
[Authorize]
public class TenantMemberController : ControllerBase
{
    private readonly IIdpService _idp;
    private readonly IOrgContextService _orgContext;

    public TenantMemberController(IIdpService idp, IOrgContextService orgContext)
    {
        _idp = idp;
        _orgContext = orgContext;
    }

    /// <summary>当前用户的待确认邀请。</summary>
    [HttpGet("pending-invites")]
    public async Task<ApiResult<List<TenantInviteDto>>> GetPendingInvites()
    {
        var userId = GetUserId();
        if (userId == null) return ApiResult<List<TenantInviteDto>>.Fail("未登录", 401);
        return ApiResult<List<TenantInviteDto>>.Success(await _idp.GetPendingInvitesAsync(userId.Value));
    }

    /// <summary>邀请用户加入租户（发起人须为该租户的已接受成员）。</summary>
    [HttpPost("invite")]
    public async Task<ApiResult<bool>> Invite([FromBody] InviteMemberRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return ApiResult<bool>.Fail("未登录", 401);

        // 发起人须为该租户成员，方可邀请他人进入本租户。
        if (!await _orgContext.ValidateTenantMembershipAsync(userId.Value, request.TenantId))
            return ApiResult<bool>.Fail("只有该租户成员可发起邀请", 403);

        await _idp.InviteMemberAsync(userId.Value, request.TargetUserId, request.TenantId, request.IsPrimary);
        return ApiResult<bool>.Success(true);
    }

    /// <summary>接受邀请（仅本人）。</summary>
    [HttpPost("accept")]
    public async Task<ApiResult<bool>> Accept([FromBody] TenantMemberActionRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return ApiResult<bool>.Fail("未登录", 401);
        await _idp.AcceptInviteAsync(userId.Value, request.TenantId);
        return ApiResult<bool>.Success(true);
    }

    /// <summary>拒绝邀请（仅本人）。</summary>
    [HttpPost("reject")]
    public async Task<ApiResult<bool>> Reject([FromBody] TenantMemberActionRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return ApiResult<bool>.Fail("未登录", 401);
        await _idp.RejectInviteAsync(userId.Value, request.TenantId);
        return ApiResult<bool>.Success(true);
    }

    private long? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}
