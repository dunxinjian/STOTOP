using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Services;

namespace STOTOP.Module.System.Filters;

/// <summary>
/// 平台层端点门禁（多租户阶段4B）。挂在 /api/platform/* 控制器/动作上，一体完成三件事：
/// <list type="number">
/// <item>【授权】校验当前用户 SYS用户.F是否平台超管=1，否则 403（平台超管 ≠ 租户内 admin）。</item>
/// <item>【物理脱离租户过滤器】进入 <see cref="IPlatformScopeFactory"/> 平台作用域——本请求内 DbContext 跳过租户硬墙，
///       可跨租户读写（design/23 §6.4"跨租户访问唯一入口 IPlatformScopeFactory.Enter"）。动作结束即 Dispose 复位。</item>
/// <item>【审计】Enter 内部写 PlatformScopeEnter 安全审计（best-effort），满足 PlatformAuditMiddleware 意图。</item>
/// </list>
/// 注意：/api/platform/* 已在 OrgContextMiddleware.SkipPaths → 不解析租户/组织上下文（CurrentTenantId 为 null），
/// 故平台动作里的 ITenantScoped 读依赖本作用域放行；未挂本过滤器的平台动作会因 fail-closed 读空集而自曝（不静默越权）。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class PlatformOnlyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? context.HttpContext.User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedObjectResult(new { code = 401, message = "未登录" });
            return;
        }

        var sp = context.HttpContext.RequestServices;
        var admin = sp.GetRequiredService<IAdminAuthorizationService>();
        var db = sp.GetRequiredService<STOTOPDbContext>();

        if (!await admin.IsPlatformAdminByUserIdAsync(db, userId))
        {
            context.Result = new ObjectResult(new { code = 403, message = "无平台超管权限" }) { StatusCode = 403 };
            return;
        }

        // 进入平台作用域（跳过租户硬墙 + 审计），动作全程有效，结束复位。
        var platformScope = sp.GetRequiredService<IPlatformScopeFactory>();
        using (platformScope.Enter($"platform-api:{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}"))
        {
            await next();
        }
    }
}
