namespace STOTOP.Module.System.Services.Interfaces;

/// <summary>
/// 钉钉 per-tenant 配置解析（多租户阶段4·钉钉地基）。抽象"根租户=JSON全局(向后兼容) / 非根租户=SYS钉钉配置表"两套来源，
/// 供 <see cref="IDingTalkService"/> 逐租户同步、以及管理台读写。<see cref="DingTalkConfigRecord"/> 为统一 DTO。
/// </summary>
public interface IDingTalkTenantConfigService
{
    /// <summary>当前上下文租户的钉钉配置；无上下文回退根租户。无配置返回 null。</summary>
    Task<DingTalkConfigRecord?> GetForCurrentTenantAsync();

    /// <summary>指定租户的钉钉配置：根租户读 JSON 全局(权威)；非根租户读 SYS钉钉配置 表。无则 null。</summary>
    Task<DingTalkConfigRecord?> GetForTenantAsync(long tenantId);

    /// <summary>upsert 指定租户配置：根租户写 JSON 全局；非根租户写 SYS钉钉配置 表(平台作用域放行 + 显式 F租户ID)。</summary>
    Task UpsertForTenantAsync(long tenantId, DingTalkConfigRecord config);

    /// <summary>仅更新当前租户配置的"最后同步时间"（根=JSON / 非根=表）。</summary>
    Task TouchLastSyncForCurrentTenantAsync();
}
