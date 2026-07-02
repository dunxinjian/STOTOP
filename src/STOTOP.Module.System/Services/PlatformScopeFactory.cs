using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;

namespace STOTOP.Module.System.Services;

/// <summary>
/// <see cref="IPlatformScopeFactory"/> 的默认实现：翻转当前作用域 <see cref="IOrgContextAccessor.IsPlatformScope"/>。
/// 与 <see cref="IOrgContextAccessor"/> 同为 Scoped 注册——共享同一实例，故 <see cref="STOTOP.Infrastructure"/> 的
/// STOTOPDbContext 立即读到置位（fail-closed 硬墙短路）。可重入：Dispose 复位为进入前的值。
/// <para>
/// M7 硬化：每次 <see cref="Enter"/> 写一条 <c>PlatformScopeEnter</c> 安全审计，让"跳过租户硬墙"这一高权操作
/// 可事后追溯。审计为 <b>best-effort</b>——绝不因审计失败（如全新库首启审计表尚未建）中断平台操作。
/// 现阶段平台作用域只出现在启动/种子/CLI 链路（admin 保持租户内、请求路径不进平台作用域），故 account 记 "system"。
/// </para>
/// </summary>
public sealed class PlatformScopeFactory : IPlatformScopeFactory
{
    private readonly IOrgContextAccessor _accessor;
    private readonly ILogger<PlatformScopeFactory> _logger;
    private readonly ISecurityAuditService _audit;
    private readonly IConfiguration _configuration;

    public PlatformScopeFactory(
        IOrgContextAccessor accessor,
        ILogger<PlatformScopeFactory> logger,
        ISecurityAuditService audit,
        IConfiguration configuration)
    {
        _accessor = accessor;
        _logger = logger;
        _audit = audit;
        _configuration = configuration;
    }

    public IDisposable Enter(string reason)
    {
        var previous = _accessor.IsPlatformScope;
        _accessor.IsPlatformScope = true;
        _logger.LogInformation("进入平台作用域（跳过租户硬墙）：{Reason}", reason);
        WriteAuditBestEffort(reason);
        return new PlatformScope(() =>
        {
            _accessor.IsPlatformScope = previous;
            _logger.LogInformation("离开平台作用域：{Reason}", reason);
        });
    }

    /// <summary>best-effort 写平台作用域进入审计——受配置开关控制，任何异常仅告警不抛。</summary>
    private void WriteAuditBestEffort(string reason)
    {
        // 灰度开关：默认开；可经 appsettings Security:AuditPlatformBypass=false 关闭（不重发布）。
        if (_configuration.GetValue<bool?>("Security:AuditPlatformBypass") == false)
            return;

        try
        {
            _audit.LogEvent(
                userId: null,
                account: "system",
                eventType: "PlatformScopeEnter",
                eventResult: "Success",
                extraData: reason).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // best-effort：审计写失败（如全新库审计表未建/连接不可用）不得中断平台操作。
            _logger.LogWarning(ex, "平台作用域审计写入失败（best-effort 忽略）：{Reason}", reason);
        }
    }

    private sealed class PlatformScope : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;
        public PlatformScope(Action onDispose) => _onDispose = onDispose;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onDispose();
        }
    }
}
