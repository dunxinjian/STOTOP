using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text.Json;

namespace STOTOP.Module.System.Middleware;

public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;

    // 跳过验证的路径前缀
    private static readonly string[] SkipPaths = new[]
    {
        "/api/auth/login",
        "/api/auth/refresh-token",
        "/api/auth/dingtalk",
        "/health",
        "/swagger"
    };

    public SessionValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // 跳过不需要验证的路径
        if (SkipPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // 只对已认证请求进行会话验证
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // 从Claims中提取sessionId
        var sessionId = context.User.FindFirst("sessionId")?.Value;
        if (string.IsNullOrEmpty(sessionId))
        {
            // 兼容旧token（无sessionId claim的token放行）
            await _next(context);
            return;
        }

        var sessionService = context.RequestServices.GetRequiredService<Services.SessionService>();
        var session = await sessionService.ValidateSession(sessionId);

        if (session == null)
        {
            // 查询会话实际状态以区分失效原因
            var (status, logoutReason) = await sessionService.GetSessionStatusInfo(sessionId);

            string code;
            string msg;
            if (status == 2)
            {
                // FStatus=2: 被其他会话踢出
                code = "SESSION_KICKED";
                msg = logoutReason ?? "您的账号已在其他设备登录";
            }
            else if (status == 0)
            {
                // FStatus=0: 主动退出或管理员终止
                code = "SESSION_EXPIRED";
                msg = "登录已过期，请重新登录";
            }
            else
            {
                // 不存在或其他
                code = "SESSION_INVALID";
                msg = "会话已失效，请重新登录";
            }

            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(new { code, message = msg });
            await context.Response.WriteAsync(json);
            return;
        }

        // 获取当前IP
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            ip = forwarded.Split(',')[0].Trim();

        var securityConfigService = context.RequestServices.GetRequiredService<Services.SecurityConfigService>();

        // 检测IP变化（同子网豁免）
        if (!string.IsNullOrEmpty(session.FIpAddress) && !string.IsNullOrEmpty(ip)
            && session.FIpAddress != ip && !IsSameSubnet(session.FIpAddress, ip))
        {
            var ipChangeForceReauth = await securityConfigService.GetBoolConfig("security.ip_change_force_reauth", false);
            if (ipChangeForceReauth)
            {
                await sessionService.TerminateSession(sessionId, "IP地址变化");
                var auditService = context.RequestServices.GetRequiredService<Services.SecurityAuditService>();
                var userId = long.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (long?)null;
                await auditService.LogEvent(userId, context.User.FindFirst(ClaimTypes.Name)?.Value,
                    "AnomalyDetected", "Success", ipAddress: ip, sessionId: sessionId,
                    extraData: $"IP变化: {session.FIpAddress} -> {ip}");

                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(new
                    {
                        code = "ANOMALY_IP_CHANGE",
                        message = "检测到登录环境变化，请重新登录"
                    }));
                return;
            }
        }

        // 检测设备指纹变化
        var deviceFingerprint = context.Request.Headers["X-Device-Fingerprint"].FirstOrDefault();
        if (!string.IsNullOrEmpty(session.FDeviceFingerprint) && !string.IsNullOrEmpty(deviceFingerprint))
        {
            var matchMode = await securityConfigService.GetStringConfig("security.fingerprint_match_mode", "loose");
            bool fingerprintChanged;

            if (matchMode == "disabled")
            {
                fingerprintChanged = false;
            }
            else if (matchMode == "loose")
            {
                // loose 模式：仅比较前32字符
                var len = Math.Min(32, Math.Min(session.FDeviceFingerprint.Length, deviceFingerprint.Length));
                fingerprintChanged = !session.FDeviceFingerprint[..len].Equals(deviceFingerprint[..len], StringComparison.Ordinal);
            }
            else
            {
                // strict 模式：完整比较
                fingerprintChanged = session.FDeviceFingerprint != deviceFingerprint;
            }

            if (fingerprintChanged)
            {
                var fingerprintChangeForceReauth = await securityConfigService.GetBoolConfig("security.fingerprint_change_force_reauth", false);
                if (fingerprintChangeForceReauth)
                {
                    await sessionService.TerminateSession(sessionId, "设备指纹变化");
                    var auditService = context.RequestServices.GetRequiredService<Services.SecurityAuditService>();
                    var userId = long.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (long?)null;
                    await auditService.LogEvent(userId, context.User.FindFirst(ClaimTypes.Name)?.Value,
                        "AnomalyDetected", "Success", ipAddress: ip, deviceFingerprint: deviceFingerprint,
                        sessionId: sessionId, extraData: $"设备指纹变化: {session.FDeviceFingerprint} -> {deviceFingerprint}");

                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(new
                        {
                            code = "ANOMALY_DEVICE_CHANGE",
                            message = "检测到设备变化，请重新登录"
                        }));
                    return;
                }
            }
        }

        // 通过验证
        await _next(context);
    }

    /// <summary>
    /// 判断两个 IP 是否在同一 /24 子网（IPv4）或 /64 子网（IPv6）
    /// </summary>
    private static bool IsSameSubnet(string ip1, string ip2)
    {
        if (IPAddress.TryParse(ip1, out var addr1) && IPAddress.TryParse(ip2, out var addr2))
        {
            // 仅处理 IPv4
            if (addr1.AddressFamily == AddressFamily.InterNetwork
                && addr2.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes1 = addr1.GetAddressBytes();
                var bytes2 = addr2.GetAddressBytes();
                // /24 子网：前3字节一致
                return bytes1[0] == bytes2[0] && bytes1[1] == bytes2[1] && bytes1[2] == bytes2[2];
            }
            // IPv6: 比较前 8 字节 (/64)
            if (addr1.AddressFamily == AddressFamily.InterNetworkV6
                && addr2.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var bytes1 = addr1.GetAddressBytes();
                var bytes2 = addr2.GetAddressBytes();
                for (int i = 0; i < 8; i++)
                {
                    if (bytes1[i] != bytes2[i]) return false;
                }
                return true;
            }
        }
        return false;
    }
}
