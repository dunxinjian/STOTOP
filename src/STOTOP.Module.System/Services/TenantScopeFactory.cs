using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;

namespace STOTOP.Module.System.Services;

/// <summary>
/// <see cref="ITenantScopeFactory"/> 的默认实现：设当前作用域 <see cref="IOrgContextAccessor.CurrentTenantId"/>。
/// 与 <see cref="IOrgContextAccessor"/> 同为 Scoped——共享同一实例（override 存静态 AsyncLocal，穿透子 DI 作用域），
/// 故 STOTOPDbContext 立即读到新租户，租户硬墙精确收敛到该租户。
/// <para>
/// 只设 CurrentTenantId、<b>不碰 IsPlatformScope</b>——这是与 <see cref="PlatformScopeFactory"/> 的本质区别：
/// 平台工厂放行全库（跨租户），本工厂收敛单租户（供逐租户迭代 / 批次链）。Dispose 复位为进入前值（可重入 / 可嵌套）。
/// </para>
/// </summary>
public sealed class TenantScopeFactory : ITenantScopeFactory
{
    private readonly IOrgContextAccessor _accessor;
    private readonly ILogger<TenantScopeFactory> _logger;

    public TenantScopeFactory(IOrgContextAccessor accessor, ILogger<TenantScopeFactory> logger)
    {
        _accessor = accessor;
        _logger = logger;
    }

    public IDisposable Enter(long tenantId, string reason)
    {
        var previous = _accessor.CurrentTenantId;
        _accessor.CurrentTenantId = tenantId;
        _logger.LogDebug("进入租户作用域 tenant={TenantId}：{Reason}", tenantId, reason);
        return new TenantScope(() =>
        {
            _accessor.CurrentTenantId = previous;
            _logger.LogDebug("离开租户作用域 tenant={TenantId}：{Reason}", tenantId, reason);
        });
    }

    private sealed class TenantScope : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;
        public TenantScope(Action onDispose) => _onDispose = onDispose;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onDispose();
        }
    }
}
