namespace STOTOP.Core.Services;

/// <summary>
/// 受控平台作用域工厂（设计 §6.2 / design/24 §阶段1）。
/// <para>
/// 唯一被批准的「跳过租户 fail-closed 硬墙」的入口：仅用于平台层 / 启动期 seeder / 数据库迁移 /
/// 跨租户平台服务等确有必要绕过 <see cref="IOrgContextAccessor.IsPlatformScope"/> 的受控场景。
/// </para>
/// <para>
/// 用法：<c>using (platformScope.Enter("startup-migration")) { /* 期间 ITenantScoped 读放行、写回填不 throw */ }</c>。
/// 进入置位、离开（Dispose）复位为进入前的值（可重入安全），并写审计日志。
/// </para>
/// <para>
/// ⚠️ 业务 Service 不应注入本工厂来绕过租户隔离——那是越权后门。阶段4 平台层落地后将收紧为类型受限可见性。
/// </para>
/// </summary>
public interface IPlatformScopeFactory
{
    /// <summary>
    /// 进入平台作用域：在返回的 <see cref="IDisposable"/> 生命周期内，当前作用域的租户硬墙被放行
    /// （<see cref="IOrgContextAccessor.IsPlatformScope"/> 置 true）。Dispose 时复位为进入前的值。
    /// </summary>
    /// <param name="reason">进入原因（审计用，如 "startup-migration" / "voucher-accountset-backfill"）。</param>
    IDisposable Enter(string reason);
}
