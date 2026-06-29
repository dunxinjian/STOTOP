using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.System.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _permission;

    public RequirePermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedObjectResult(new { code = 401, message = "未登录" });
            return;
        }

        // 使用集中的admin检查服务（从Claim读取，无DB查询）
        var adminService = context.HttpContext.RequestServices.GetRequiredService<IAdminAuthorizationService>();
        if (adminService.IsAdmin(context.HttpContext.User))
        {
            await next();
            return;
        }

        // 权限查询
        var db = context.HttpContext.RequestServices.GetRequiredService<STOTOPDbContext>();
        var count = await db.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(1) AS [Value] FROM [SYS用户角色] ur " +
                "JOIN [SYS角色权限] rp ON ur.F角色ID = rp.F角色ID " +
                "JOIN [SYS功能权限] p ON rp.F权限ID = p.FID " +
                "WHERE ur.F用户ID = {0} AND p.F编码 = {1}",
                userId, _permission)
            .FirstOrDefaultAsync();

        if (count == 0)
        {
            context.Result = new ObjectResult(new { code = 403, message = $"无操作权限：{_permission}" })
            {
                StatusCode = 403
            };
            return;
        }

        await next();
    }
}
