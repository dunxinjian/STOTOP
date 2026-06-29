using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;
using System.Security.Claims;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/org-context")]
[ApiController]
[Authorize]
public class OrgContextController : ControllerBase
{
    private readonly IOrgContextService _orgContextService;

    public OrgContextController(IOrgContextService orgContextService)
    {
        _orgContextService = orgContextService;
    }

    [HttpGet("my-organizations")]
    public async Task<ApiResult<List<UserOrganizationDto>>> GetMyOrganizations()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return ApiResult<List<UserOrganizationDto>>.Fail("未登录", 401);

        var result = await _orgContextService.GetUserOrganizationsAsync(userId.Value);
        return ApiResult<List<UserOrganizationDto>>.Success(result);
    }

    [HttpPost("switch")]
    public async Task<ApiResult<SwitchOrganizationResponse>> SwitchOrganization([FromBody] SwitchOrganizationRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return ApiResult<SwitchOrganizationResponse>.Fail("未登录", 401);

        var result = await _orgContextService.SwitchOrganizationAsync(userId.Value, request.OrgId);
        return ApiResult<SwitchOrganizationResponse>.Success(result);
    }

    [HttpGet("current")]
    public async Task<ApiResult<SwitchOrganizationResponse?>> GetCurrentContext()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return ApiResult<SwitchOrganizationResponse?>.Fail("未登录", 401);

        var currentOrgId = HttpContext.Items["CurrentOrgId"] as long?;
        if (currentOrgId == null)
        {
            return ApiResult<SwitchOrganizationResponse?>.Fail("未选择组织", 428);
        }

        var result = await _orgContextService.SwitchOrganizationAsync(userId.Value, currentOrgId.Value);
        return ApiResult<SwitchOrganizationResponse?>.Success(result);
    }

    [HttpPost("user-organizations")]
    public async Task<ApiResult<bool>> AddUserToOrganization([FromBody] AddUserToOrganizationRequest request)
    {
        await _orgContextService.AddUserToOrganizationAsync(request);
        return ApiResult<bool>.Success(true);
    }

    [HttpPut("user-organizations/{id}")]
    public async Task<ApiResult<bool>> UpdateUserOrganization(long id, [FromBody] UpdateUserOrganizationRequest request)
    {
        await _orgContextService.UpdateUserOrganizationAsync(id, request);
        return ApiResult<bool>.Success(true);
    }

    [HttpDelete("user-organizations/{id}")]
    public async Task<ApiResult<bool>> RemoveUserFromOrganization(long id)
    {
        await _orgContextService.RemoveUserFromOrganizationAsync(id);
        return ApiResult<bool>.Success(true);
    }

    private long? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return null;
        return userId;
    }
}
