using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Module.System.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace STOTOP.Module.System.Middleware;

/// <summary>
/// 滑动续期中间件：当 JWT 剩余有效期 ≤ 阈值时，自动签发新 Token 写入响应头
/// </summary>
public class TokenSlidingRefreshMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly string[] SkipPaths = new[]
    {
        "/api/auth/login",
        "/api/auth/refresh-token",
        "/api/auth/logout",
        "/health",
        "/swagger",
        "/hangfire"
    };

    public TokenSlidingRefreshMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 只在请求成功认证后、响应前检查
        await _next(context);

        // 跳过条件
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (SkipPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return;

        // 非成功响应不续期
        if (context.Response.StatusCode >= 400)
            return;

        // 从 Claims 提取 Token 过期时间
        var expClaim = context.User.FindFirst(JwtRegisteredClaimNames.Exp)
            ?? context.User.FindFirst("exp");
        if (expClaim == null || !long.TryParse(expClaim.Value, out var expUnix))
            return;

        var expTime = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        var remaining = expTime - DateTimeOffset.UtcNow;

        // 读取阈值配置
        var securityConfigService = context.RequestServices.GetService<Services.SecurityConfigService>();
        var thresholdMinutes = 10; // 默认10分钟
        if (securityConfigService != null)
        {
            thresholdMinutes = await securityConfigService.GetIntConfig("token.sliding_refresh_threshold_minutes", 10);
        }

        // 剩余时间大于阈值，无需续期
        if (remaining.TotalMinutes > thresholdMinutes)
            return;

        // 已经过期的 Token 不在此处理（交由 401 流程）
        if (remaining.TotalSeconds <= 0)
            return;

        // 签发新 Token
        var userId = long.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("userId")?.Value, out var uid) ? uid : 0;
        var sessionId = context.User.FindFirst("sessionId")?.Value;

        if (userId == 0)
            return;

        try
        {
            var authService = context.RequestServices.GetRequiredService<IAuthService>();
            var (newToken, expiresIn) = await authService.IssueAccessTokenAsync(userId, sessionId);
            if (!string.IsNullOrEmpty(newToken))
            {
                context.Response.Headers["X-New-Token"] = newToken;
                context.Response.Headers["X-Token-Expires-In"] = expiresIn.ToString();
            }
        }
        catch
        {
            // 续期失败不应影响正常请求
        }
    }
}
