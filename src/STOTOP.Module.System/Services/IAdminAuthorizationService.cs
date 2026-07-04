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
}
