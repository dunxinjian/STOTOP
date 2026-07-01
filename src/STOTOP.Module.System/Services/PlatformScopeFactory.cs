using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;

namespace STOTOP.Module.System.Services;

/// <summary>
/// <see cref="IPlatformScopeFactory"/> 的默认实现：翻转当前作用域 <see cref="IOrgContextAccessor.IsPlatformScope"/>。
/// 与 <see cref="IOrgContextAccessor"/> 同为 Scoped 注册——共享同一实例，故 <see cref="STOTOP.Infrastructure"/> 的
/// STOTOPDbContext 立即读到置位（fail-closed 硬墙短路）。可重入：Dispose 复位为进入前的值。
/// </summary>
public sealed class PlatformScopeFactory : IPlatformScopeFactory
{
    private readonly IOrgContextAccessor _accessor;
    private readonly ILogger<PlatformScopeFactory> _logger;

    public PlatformScopeFactory(IOrgContextAccessor accessor, ILogger<PlatformScopeFactory> logger)
    {
        _accessor = accessor;
        _logger = logger;
    }

    public IDisposable Enter(string reason)
    {
        var previous = _accessor.IsPlatformScope;
        _accessor.IsPlatformScope = true;
        _logger.LogInformation("进入平台作用域（跳过租户硬墙）：{Reason}", reason);
        return new PlatformScope(() =>
        {
            _accessor.IsPlatformScope = previous;
            _logger.LogInformation("离开平台作用域：{Reason}", reason);
        });
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
