using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.System.Services;
using System.Reflection;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/account-set-auth")]
public class AccountSetAuthController : ControllerBase
{
    private readonly IAccountSetAuthorizationService _authService;

    public AccountSetAuthController(IAccountSetAuthorizationService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// 获取账套已授权用户列表
    /// </summary>
    [HttpGet("{accountSetId}/users")]
    public async Task<IActionResult> GetAccountSetUsers(long accountSetId)
    {
        var users = await _authService.GetAccountSetUsersAsync(accountSetId);
        return Ok(new { code = 0, data = users });
    }

    /// <summary>
    /// 授权用户访问账套
    /// </summary>
    [HttpPost("grant")]
    [RequirePermission(AccountSetPermissions.AuthorizationManage)]
    public async Task<IActionResult> Grant([FromBody] GrantAccountSetRequest request)
    {
        var grantedBy = GetCurrentUserId();
        var orgId = GetCurrentOrgId();

        var id = await _authService.GrantAsync(
            request.UserId,
            request.AccountSetId,
            request.AccountSetRoleId,
            orgId,
            grantedBy);

        return Ok(new { code = 0, data = new { id }, message = "授权成功" });
    }

    /// <summary>
    /// 修改授权角色
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(AccountSetPermissions.AuthorizationManage)]
    public async Task<IActionResult> UpdateRole(long id, [FromBody] UpdateAccountSetRoleRequest request)
    {
        var result = await _authService.UpdateRoleAsync(id, request.AccountSetRoleId);
        if (!result)
            return Ok(new { code = -1, message = "授权记录不存在" });
        return Ok(new { code = 0, message = "修改成功" });
    }

    /// <summary>
    /// 撤销授权
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermission(AccountSetPermissions.AuthorizationManage)]
    public async Task<IActionResult> Revoke(long id)
    {
        var result = await _authService.RevokeAsync(id);
        if (!result)
            return Ok(new { code = -1, message = "授权记录不存在" });
        return Ok(new { code = 0, message = "已撤销授权" });
    }

    /// <summary>
    /// 获取所有可用账套角色
    /// </summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _authService.GetRolesAsync();
        return Ok(new { code = 0, data = roles });
    }

    /// <summary>
    /// 获取当前用户对指定账套的权限列表
    /// </summary>
    [HttpGet("my-permissions/{accountSetId}")]
    public async Task<IActionResult> GetMyPermissions(long accountSetId)
    {
        var userId = GetCurrentUserId();

        // admin 返回所有权限
        var adminService = HttpContext.RequestServices.GetRequiredService<IAdminAuthorizationService>();
        if (adminService.IsAdmin(User))
        {
            var allPermissions = typeof(AccountSetPermissions)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToList();
            return Ok(new { code = 0, data = allPermissions });
        }

        var permissions = await _authService.GetUserPermissionsAsync(userId, accountSetId);
        return Ok(new { code = 0, data = permissions });
    }

    private long GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdStr) && long.TryParse(userIdStr, out var userId))
            return userId;
        return 0;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = HttpContext.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }
}
