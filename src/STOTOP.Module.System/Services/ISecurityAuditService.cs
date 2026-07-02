namespace STOTOP.Module.System.Services;

/// <summary>
/// 安全审计日志写入契约。抽出接口以便平台旁路审计（<see cref="PlatformScopeFactory"/> /
/// OrgContextMiddleware）依赖抽象、可在隔离自检中以假实现验证"是否写审计"，而无需真实 SQL 连接。
/// 具体实现 <see cref="SecurityAuditService"/> 经 Dapper 直插 [SYS安全审计日志]（绕 EF 查询过滤器，
/// 不受 fail-closed 租户硬墙影响，故启动期/平台作用域下亦可写）。
/// </summary>
public interface ISecurityAuditService
{
    /// <summary>记录一条安全审计事件（失败由调用方按 best-effort 处置，勿让审计失败中断主流程）。</summary>
    Task LogEvent(long? userId, string? account, string eventType, string eventResult,
        string? ipAddress = null, string? deviceFingerprint = null, string? deviceInfo = null,
        string? failReason = null, string? sessionId = null, string? extraData = null);
}
