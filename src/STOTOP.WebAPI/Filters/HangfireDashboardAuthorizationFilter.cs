using System.Net;
using System.Security.Claims;
using Hangfire.Dashboard;
using STOTOP.Module.System.Services;

namespace STOTOP.WebAPI.Filters;

/// <summary>
/// Hangfire 仪表盘授权过滤器。
///
/// 仅允许：
///   1) 本地回环请求（开发期 Vite 代理、部署后在服务器本机访问）；
///   2) 已认证的管理员（携带有效 JWT；/hangfire 的 access_token 查询串经 JwtBearer 处理后会填充 User）。
///
/// 显式声明授权策略，不再依赖 Hangfire 隐式的"仅本地"默认——避免将来一旦启用 UseForwardedHeaders/反向代理
/// 导致仪表盘被匿名远程访问、进而触发或删除任意定时任务（备份/薪酬/积分/计费等）。
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp != null && IPAddress.IsLoopback(remoteIp))
            return true;

        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated == true &&
            user.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == AdminAuthorizationService.AdminRoleClaim))
            return true;

        return false;
    }
}
