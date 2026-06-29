using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.Finance.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequireAccountSetPermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string[] _permissionCodes;

    public RequireAccountSetPermissionAttribute(params string[] permissionCodes)
    {
        _permissionCodes = permissionCodes;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1. 获取 userId
        var userIdStr = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            context.Result = new ObjectResult(new { code = 401, message = "未认证" }) { StatusCode = 401 };
            return;
        }

        // 2. 拥有管理员角色的用户直接放行
        var db = context.HttpContext.RequestServices.GetRequiredService<STOTOPDbContext>();
        var adminService = context.HttpContext.RequestServices.GetRequiredService<IAdminAuthorizationService>();
        if (adminService.IsAdmin(context.HttpContext.User))
        {
            await next();
            return;
        }

        // 3. 获取 accountSetId - 优先从请求头获取，其次从路由参数
        long accountSetId = 0;
        var accountSetHeader = context.HttpContext.Request.Headers["X-AccountSet-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(accountSetHeader))
        {
            long.TryParse(accountSetHeader, out accountSetId);
        }

        // 也尝试从路由参数 accountSetId 获取
        if (accountSetId == 0 && context.ActionArguments.ContainsKey("accountSetId"))
        {
            var argVal = context.ActionArguments["accountSetId"];
            if (argVal is long asId) accountSetId = asId;
            else if (argVal is int asIdInt) accountSetId = asIdInt;
        }

        if (accountSetId <= 0)
        {
            context.Result = new ObjectResult(
                new { code = 400, message = "缺少有效的账套ID" }
            ) { StatusCode = 400 };
            return;
        }

        // 4. 检查权限（任一权限码命中即放行）
        var authService = context.HttpContext.RequestServices.GetRequiredService<IAccountSetAuthorizationService>();
        var hasPermission = false;
        foreach (var code in _permissionCodes)
        {
            if (await authService.HasPermissionAsync(userId, accountSetId, code))
            {
                hasPermission = true;
                break;
            }
        }

        if (!hasPermission)
        {
            context.Result = new ObjectResult(new { code = 403, message = $"无账套操作权限：{string.Join("/", _permissionCodes)}" })
            {
                StatusCode = 403
            };
            return;
        }

        await next();
    }
}
