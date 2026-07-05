namespace STOTOP.Core.Services;

/// <summary>
/// 租户级作用域工厂（design/23 §8：非 HTTP 入口须经 Enter 显式固化租户上下文）。
/// <para>
/// 区别于 <see cref="IPlatformScopeFactory"/>——后者置 <see cref="IOrgContextAccessor.IsPlatformScope"/>=true
/// 放行全库(跨租户可见)；本工厂只设 <see cref="IOrgContextAccessor.CurrentTenantId"/>(不碰 IsPlatformScope)，
/// 令租户 fail-closed 硬墙过滤器精确收敛到该单一租户。专供后台 / 批次 / per-tenant 迭代等无 HttpContext 链路
/// 逐租户处理，绝不用平台旁路（那会串租户、写入回填串号）。
/// </para>
/// <para>
/// 用法：<c>using (tenantScope.Enter(tid, "ksf-calc")) { /* 期间读写仅限该租户 */ }</c>。
/// 进入设值、离开（Dispose）复位为进入前的值（可重入 / 可嵌套安全，与 <see cref="IPlatformScopeFactory"/> 同范式）。
/// </para>
/// </summary>
public interface ITenantScopeFactory
{
    /// <summary>
    /// 固化 <paramref name="tenantId"/> 到当前作用域的 <see cref="IOrgContextAccessor.CurrentTenantId"/>；
    /// 返回 <see cref="IDisposable"/>，Dispose 时复位为进入前的值。
    /// </summary>
    /// <param name="tenantId">目标租户（PLT租户.FID）。</param>
    /// <param name="reason">进入原因（日志用，如 Job id / 批次链标识）。</param>
    IDisposable Enter(long tenantId, string reason);
}
