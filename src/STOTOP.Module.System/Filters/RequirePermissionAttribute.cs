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

        // 使用集中的admin检查服务（从Claim读取，无DB查询）——OA_ADMIN 仅平台级 admin 持有（R5·stage4C）。
        var adminService = context.HttpContext.RequestServices.GetRequiredService<IAdminAuthorizationService>();
        if (adminService.IsAdmin(context.HttpContext.User))
        {
            await next();
            return;
        }

        // R5·stage4C：租户级 admin（不签 OA_ADMIN）在其作用域内功能全量放行；
        // 跨租户读写由管理类服务层租户数据墙 + [PlatformOnly] 兜住，故此处放行功能门禁是安全的。
        if (context.HttpContext.User.HasClaim("tenantAdmin", "1"))
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
