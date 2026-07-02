using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Models;
using STOTOP.Core.Services;
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

        // v2 多租户：设当前租户(客户)。过渡期(单客户)=组织树根 id；区域公司间在租户内用 R8 数据范围。
        var tenantResolver = context.RequestServices.GetRequiredService<ITenantResolver>();
        var currentTenantId = tenantResolver.GetRootTenantId();
        if (currentTenantId.HasValue)
            context.Items["CurrentTenantId"] = currentTenantId.Value;

        // 从请求头读取组织ID
        var orgContextHeader = context.Request.Headers["X-Org-Context"].FirstOrDefault();

        if (!string.IsNullOrEmpty(orgContextHeader) && long.TryParse(orgContextHeader, out var orgId))
        {
            // admin 用户跳过组织归属验证，直接设置。
            // M7 硬化：admin 保持"租户内"——仅采信组织覆盖，租户硬墙仍作用、【不】进平台作用域（避免越权跨租户）；
            // 但此覆盖属高权旁路，对变更类请求写审计以可追溯（best-effort，见下方助手）。
            var adminService = context.RequestServices.GetRequiredService<IAdminAuthorizationService>();
            if (adminService.IsAdmin(context.User))
            {
                context.Items["CurrentOrgId"] = orgId;
                await AuditAdminOrgOverrideBestEffortAsync(context, userId, orgId);
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

    /// <summary>
    /// best-effort 记录 admin 组织覆盖审计：仅对变更类方法（POST/PUT/DELETE/PATCH）记录以压噪（GET 浏览不写）；
    /// 受 <c>Security:AuditPlatformBypass</c> 开关控制（默认开）；任何异常吞掉、绝不影响 admin 请求。
    /// </summary>
    private static async Task AuditAdminOrgOverrideBestEffortAsync(HttpContext context, long userId, long orgId)
    {
        var method = context.Request.Method;
        if (!HttpMethods.IsPost(method) && !HttpMethods.IsPut(method)
            && !HttpMethods.IsDelete(method) && !HttpMethods.IsPatch(method))
            return;

        var config = context.RequestServices.GetService<IConfiguration>();
        if (config?.GetValue<bool?>("Security:AuditPlatformBypass") == false)
            return;

        try
        {
            var audit = context.RequestServices.GetRequiredService<ISecurityAuditService>();
            var account = context.User.FindFirst(ClaimTypes.Name)?.Value
                       ?? context.User.FindFirst("userName")?.Value;
            var extra = JsonSerializer.Serialize(
                new { orgId, method, path = context.Request.Path.Value }, CamelCaseOptions);
            await audit.LogEvent(
                userId: userId,
                account: account,
                eventType: "AdminOrgOverride",
                eventResult: "Success",
                ipAddress: context.Connection.RemoteIpAddress?.ToString(),
                extraData: extra);
        }
        catch
        {
            // best-effort：审计失败不影响 admin 请求（如审计表/连接不可用）。
        }
    }
}
