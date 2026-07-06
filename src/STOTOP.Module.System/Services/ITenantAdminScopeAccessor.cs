namespace STOTOP.Module.System.Services;

/// <summary>
/// 请求级租户数据作用域（R5·stage4C 前置）：管理类接口（用户/角色/组织/岗位）据此把「非租户隔离」的 SYS 表
/// 收敛到当前登录者所辖租户。平台级 admin（OA_ADMIN）不受限；其余按 JWT 的 scopeTenantId claim，
/// claim 缺失时回退查 SYS租户成员（兼容旧 token / 未带 claim 的引导路径）。结果请求内缓存。
/// </summary>
public interface ITenantAdminScopeAccessor
{
    /// <summary>解析当前请求身份的租户数据作用域（幂等，首次解析后缓存）。</summary>
    Task<TenantDataScope> ResolveAsync();
}

/// <summary>租户数据作用域。</summary>
/// <param name="IsPlatformAdmin">平台级 admin：跨租户不受限。</param>
/// <param name="TenantIds">受限租户根 FID 集合（IsPlatformAdmin 时忽略；为空 = 看不到任何租户数据，fail-closed）。</param>
public sealed record TenantDataScope(bool IsPlatformAdmin, IReadOnlyList<long> TenantIds)
{
    /// <summary>指定租户是否在可见/可操作范围内（平台级 admin 恒真）。</summary>
    public bool Allows(long tenantId) => IsPlatformAdmin || TenantIds.Contains(tenantId);
}
