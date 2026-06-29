using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Models;
using STOTOP.Module.System.Services;
using STOTOP.Module.System.Services.Interfaces;
using System.Security.Claims;

namespace STOTOP.Module.System.Middleware;

public class OrgContextMiddleware
{
    private readonly RequestDelegate _next;

    // 不需要组织上下文的路径前缀
    private static readonly string[] SkipPaths = new[]
    {
        "/api/auth/",
        "/api/system/org-context/my-organizations",
        "/api/system/org-context/switch",
        "/setup",
        "/health",
        "/swagger",
        "/hubs/"
    };

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OrgContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // 跳过不需要组织上下文的路径
        if (ShouldSkip(path))
        {
            await _next(context);
            return;
        }

        // 未认证的请求直接放行（由认证中间件处理）
        if (context.User.Identity == null || !context.User.Identity.IsAuthenticated)
        {
            await _next(context);
            return;
        }

        // 解析当前用户ID
        var userIdStr = context.User.FindFirst("userId")?.Value
                     ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!long.TryParse(userIdStr, out var userId))
        {
            await _next(context);
            return;
        }

        // 从请求头读取组织ID
        var orgContextHeader = context.Request.Headers["X-Org-Context"].FirstOrDefault();

        if (!string.IsNullOrEmpty(orgContextHeader) && long.TryParse(orgContextHeader, out var orgId))
        {
            // admin 用户跳过组织归属验证，直接设置
            var adminService = context.RequestServices.GetRequiredService<IAdminAuthorizationService>();
            if (adminService.IsAdmin(context.User))
            {
                context.Items["CurrentOrgId"] = orgId;
                await _next(context);
                return;
            }

            // 验证当前用户属于该组织
            var orgContextService = context.RequestServices.GetRequiredService<IOrgContextService>();
            var userOrgs = await orgContextService.GetUserOrganizationsAsync(userId);
            var matchedOrg = userOrgs.FirstOrDefault(uo => uo.OrgId == orgId);

            if (matchedOrg != null)
            {
                context.Items["CurrentOrgId"] = orgId;
                await _next(context);
                return;
            }

            // 用户不属于该组织
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            var forbiddenResult = ApiResult.Fail("无权访问该组织", 403);
            await context.Response.WriteAsync(JsonSerializer.Serialize(forbiddenResult, CamelCaseOptions));
            return;
        }

        // 无值：自动推断
        var orgService = context.RequestServices.GetRequiredService<IOrgContextService>();
        var organizations = await orgService.GetUserOrganizationsAsync(userId);

        if (organizations.Count == 0)
        {
            // 没有组织，放行
            await _next(context);
            return;
        }

        if (organizations.Count == 1)
        {
            // 只有1个组织：自动设置
            context.Items["CurrentOrgId"] = organizations[0].OrgId;
            await _next(context);
            return;
        }

        // 多个组织：查找主组织
        var primaryOrg = organizations.FirstOrDefault(o => o.IsPrimaryOrg == 1);
        if (primaryOrg != null)
        {
            context.Items["CurrentOrgId"] = primaryOrg.OrgId;
            await _next(context);
            return;
        }

        // 多个组织且无主组织：返回 428 状态码（需要选择组织）
        context.Response.StatusCode = 428; // Precondition Required
        context.Response.ContentType = "application/json";
        var result = ApiResult.Fail("请先选择组织", 428);
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, CamelCaseOptions));
    }

    private static bool ShouldSkip(string path)
    {
        foreach (var skipPath in SkipPaths)
        {
            if (path.StartsWith(skipPath))
                return true;
        }
        return false;
    }
}
