using System.Security.Claims;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 集中的Admin权限检查服务，消除分散的硬编码admin判断
/// </summary>
public interface IAdminAuthorizationService
{
    /// <summary>
    /// 从JWT Claims判断是否admin（请求阶段使用，无需DB查询）
    /// </summary>
    bool IsAdmin(ClaimsPrincipal? user);

    /// <summary>
    /// 通过userId查数据库判断是否admin角色（登录阶段/Claim不可用时使用）
    /// </summary>
    Task<bool> IsAdminByUserIdAsync(STOTOPDbContext db, long userId);

    /// <summary>
    /// 通过 userId 查 SYS用户.F是否平台超管 判断是否【平台超管】（多租户阶段4）。
    /// 平台超管 ≠ 租户内 admin：仅平台超管可访问 /api/platform/* 跨租户接口。不进 JWT（撤销即时生效），每次查库。
    /// </summary>
    Task<bool> IsPlatformAdminByUserIdAsync(STOTOPDbContext db, long userId);

    /// <summary>
    /// 解析用户的管理员作用域（R5·stage4C 前置）：区分【平台级 admin】(持 F作用域=platform 的 F是否管理员 角色，如全局 role1)
    /// 与【租户级 admin】(持 F作用域=tenant 的 F是否管理员 角色)。平台级 = 跨租户全权(签 OA_ADMIN 全量短路)；
    /// 租户级 = 仅本租户，功能权限作用域内全量、跨租户由服务层数据墙 + [PlatformOnly] 兜住。
    /// SYS用户角色/SYS角色 非 IOrgScoped/ITenantScoped → LINQ 直查安全，provider-agnostic 可 InMemory 测。
    /// </summary>
    Task<AdminScope> ResolveAdminScopeAsync(STOTOPDbContext db, long userId);
}

/// <summary>
/// 管理员作用域（<see cref="IAdminAuthorizationService.ResolveAdminScopeAsync"/> 结果）。
/// </summary>
/// <param name="IsAdmin">是否持有任一管理员型角色（F是否管理员=1）。</param>
/// <param name="IsPlatformAdmin">是否平台级 admin（持 F作用域=platform 的管理员角色）。平台级 admin 一律视为 IsAdmin。</param>
/// <param name="TenantIds">租户级 admin 所辖租户根 FID（去重非零）；平台级 admin 为空（不限）；非 admin 为空。</param>
public readonly record struct AdminScope(bool IsAdmin, bool IsPlatformAdmin, IReadOnlyList<long> TenantIds);
