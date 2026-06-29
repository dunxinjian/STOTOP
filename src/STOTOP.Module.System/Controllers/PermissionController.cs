using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Filters;
using STOTOP.Module.System.Services.Interfaces;
using System.Security.Claims;

namespace STOTOP.Module.System.Controllers;

[Route("api/system/permissions")]
[ApiController]
[Authorize]
public class PermissionController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    [HttpGet("tree")]
    public async Task<ApiResult<List<PermissionDto>>> GetTree()
    {
        return await _permissionService.GetTreeAsync();
    }

    [HttpGet("menu-tree")]
    public async Task<ApiResult<List<MenuTreeResponse>>> GetMenuTree()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return ApiResult<List<MenuTreeResponse>>.Fail("未登录", 401);
        }

        return await _permissionService.GetMenuTreeAsync(userId);
    }

    [HttpGet("current")]
    public async Task<ApiResult<List<string>>> GetCurrentPermissions()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return ApiResult<List<string>>.Fail("未登录", 401);
        }

        return await _permissionService.GetCurrentPermissionsAsync(userId);
    }

    [RequirePermission(SystemPermissions.PermissionCreate)]
    [HttpPost]
    public async Task<ApiResult<PermissionDto>> Create([FromBody] CreatePermissionRequest request)
    {
        return await _permissionService.CreateAsync(request);
    }

    [RequirePermission(SystemPermissions.PermissionEdit)]
    [HttpPut("{id}")]
    public async Task<ApiResult<PermissionDto>> Update(long id, [FromBody] UpdatePermissionRequest request)
    {
        return await _permissionService.UpdateAsync(id, request);
    }

    [RequirePermission(SystemPermissions.PermissionDelete)]
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _permissionService.DeleteAsync(id);
    }
}
