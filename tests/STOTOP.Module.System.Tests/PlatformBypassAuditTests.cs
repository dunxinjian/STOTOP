using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Core.Services;
using STOTOP.Module.System.Middleware;
using STOTOP.Module.System.Services;
using Xunit;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段1 · M7【平台旁路审计优先硬化】自检：
/// ① 平台作用域进入写 <c>PlatformScopeEnter</c> 审计，且审计失败 best-effort 不阻断作用域翻转；
/// ② admin 传 X-Org-Context 覆盖组织归属时，仅采信组织、【不】进入平台作用域（锁死"admin 保持租户内"决策，
///    防将来有人把 admin 旁路包进平台作用域造成静默跨租户泄漏）。
/// </summary>
public class PlatformBypassAuditTests
{
    // 记录型审计假实现：捕获调用，供断言"是否写审计/事件类型正确"，无需真实 SQL 连接。
    private sealed class RecordingAudit : ISecurityAuditService
    {
        public List<(string EventType, string EventResult, string? ExtraData)> Calls { get; } = new();

        public global::System.Threading.Tasks.Task LogEvent(long? userId, string? account, string eventType, string eventResult,
            string? ipAddress = null, string? deviceFingerprint = null, string? deviceInfo = null,
            string? failReason = null, string? sessionId = null, string? extraData = null)
        {
            Calls.Add((eventType, eventResult, extraData));
            return global::System.Threading.Tasks.Task.CompletedTask;
        }
    }

    // 抛异常审计假实现：验证审计失败不得中断平台操作（best-effort）。
    private sealed class ThrowingAudit : ISecurityAuditService
    {
        public global::System.Threading.Tasks.Task LogEvent(long? userId, string? account, string eventType, string eventResult,
            string? ipAddress = null, string? deviceFingerprint = null, string? deviceInfo = null,
            string? failReason = null, string? sessionId = null, string? extraData = null)
            => throw new InvalidOperationException("审计下游不可用");
    }

    private sealed class FakeTenantResolver : ITenantResolver
    {
        public long? GetRootTenantId() => 1;
        public long? ResolveTenantForOrg(long orgId) => 1;
    }

    private static IConfiguration Config(bool? auditPlatformBypass)
    {
        var dict = new Dictionary<string, string?>();
        if (auditPlatformBypass.HasValue)
            dict["Security:AuditPlatformBypass"] = auditPlatformBypass.Value ? "true" : "false";
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void 平台作用域_Enter写PlatformScopeEnter审计_且翻转与恢复正确()
    {
        var accessor = new HttpOrgContextAccessor(new HttpContextAccessor());
        accessor.ClearOverride(); // 归零本执行流的静态 AsyncLocal，确保 previous=false
        var audit = new RecordingAudit();
        var factory = new PlatformScopeFactory(accessor, NullLogger<PlatformScopeFactory>.Instance, audit, Config(null));

        Assert.False(accessor.IsPlatformScope);
        using (factory.Enter("startup-migration"))
        {
            Assert.True(accessor.IsPlatformScope); // 进入即置位（DbContext 立即读到）
        }
        Assert.False(accessor.IsPlatformScope); // Dispose 复位为进入前

        var call = Assert.Single(audit.Calls);
        Assert.Equal("PlatformScopeEnter", call.EventType);
        Assert.Equal("Success", call.EventResult);
        Assert.Equal("startup-migration", call.ExtraData);
    }

    [Fact]
    public void 平台作用域_审计写入抛异常_不阻断作用域进入与恢复()
    {
        var accessor = new HttpOrgContextAccessor(new HttpContextAccessor());
        accessor.ClearOverride();
        var factory = new PlatformScopeFactory(accessor, NullLogger<PlatformScopeFactory>.Instance, new ThrowingAudit(), Config(null));

        // 审计下游抛异常也不得让 Enter 抛（best-effort）：作用域仍正确进入/恢复。
        using (factory.Enter("voucher-accountset-backfill"))
        {
            Assert.True(accessor.IsPlatformScope);
        }
        Assert.False(accessor.IsPlatformScope);
    }

    [Fact]
    public void 平台作用域_灰度关闭时不写审计()
    {
        var accessor = new HttpOrgContextAccessor(new HttpContextAccessor());
        accessor.ClearOverride();
        var audit = new RecordingAudit();
        var factory = new PlatformScopeFactory(accessor, NullLogger<PlatformScopeFactory>.Instance, audit, Config(auditPlatformBypass: false));

        using (factory.Enter("cli-init-database")) { }

        Assert.Empty(audit.Calls); // 开关关闭 → 不写审计，但作用域仍工作
    }

    // 跑一遍 admin 带 X-Org-Context=999 的请求，返回 (上下文, next期间是否处于平台作用域, next是否被调用)。
    private static async global::System.Threading.Tasks.Task<(DefaultHttpContext Ctx, bool PlatformDuringNext, bool NextCalled)>
        RunAdminOrgOverride(string method, ISecurityAuditService? audit, bool? auditGate)
    {
        // 归零静态 AsyncLocal：若将来有人误把 admin 分支包进 platformScope.Enter，_next 期间会读到 true → 断言转红。
        new HttpOrgContextAccessor(new HttpContextAccessor()).ClearOverride();

        var services = new ServiceCollection();
        services.AddScoped<IAdminAuthorizationService, AdminAuthorizationService>();
        services.AddSingleton<ITenantResolver>(new FakeTenantResolver());
        services.AddSingleton<IConfiguration>(Config(auditGate));
        if (audit != null) services.AddSingleton<ISecurityAuditService>(audit);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, AdminAuthorizationService.AdminRoleClaim), // OA_ADMIN → IsAdmin
            new Claim(ClaimTypes.Name, "admin"),
            new Claim("userId", "5"),
        }, "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        context.Request.Method = method;
        context.Request.Path = "/api/test/thing";
        context.Request.Headers["X-Org-Context"] = "999"; // 覆盖到非成员组织

        bool platformScopeDuringNext = true; // 默认 true：若 next 未被调用也会让不变量断言失败
        bool nextCalled = false;
        var middleware = new OrgContextMiddleware(_ =>
        {
            nextCalled = true;
            platformScopeDuringNext = new HttpOrgContextAccessor(new HttpContextAccessor()).IsPlatformScope;
            return global::System.Threading.Tasks.Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        return (context, platformScopeDuringNext, nextCalled);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task admin组织覆盖_采信组织但不进入平台作用域_保持租户内()
    {
        var (ctx, platformDuringNext, nextCalled) = await RunAdminOrgOverride("POST", audit: null, auditGate: false);

        Assert.True(nextCalled);
        Assert.Equal(999L, ctx.Items["CurrentOrgId"]); // admin 组织覆盖被采信
        Assert.False(platformDuringNext);              // 但【未】进入平台作用域 → admin 仍受租户硬墙约束
    }

    [Fact]
    public async global::System.Threading.Tasks.Task admin组织覆盖_变更类方法写AdminOrgOverride审计()
    {
        var audit = new RecordingAudit();
        var (ctx, _, nextCalled) = await RunAdminOrgOverride("POST", audit, auditGate: null); // 开关默认开

        Assert.True(nextCalled);
        Assert.Equal(999L, ctx.Items["CurrentOrgId"]);
        var call = Assert.Single(audit.Calls);
        Assert.Equal("AdminOrgOverride", call.EventType);
        Assert.Equal("Success", call.EventResult);
        Assert.Contains("999", call.ExtraData);                // 目标组织
        Assert.Contains("/api/test/thing", call.ExtraData!);   // 路径
    }

    [Fact]
    public async global::System.Threading.Tasks.Task admin组织覆盖_GET不写审计_压噪()
    {
        var audit = new RecordingAudit();
        var (ctx, _, nextCalled) = await RunAdminOrgOverride("GET", audit, auditGate: null);

        Assert.True(nextCalled);
        Assert.Equal(999L, ctx.Items["CurrentOrgId"]); // 组织仍采信
        Assert.Empty(audit.Calls);                     // 但 GET 不写审计（压噪）
    }

    [Fact]
    public async global::System.Threading.Tasks.Task admin组织覆盖_审计抛异常请求仍放行()
    {
        var (ctx, _, nextCalled) = await RunAdminOrgOverride("POST", new ThrowingAudit(), auditGate: null);

        Assert.True(nextCalled);                        // best-effort：审计抛异常不影响请求
        Assert.Equal(999L, ctx.Items["CurrentOrgId"]);
    }
}
