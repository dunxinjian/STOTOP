using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

public class AdminAuthorizationService : IAdminAuthorizationService
{
    /// <summary>Admin角色的数据库ID</summary>
    public const long AdminRoleId = 1;

    /// <summary>JWT中admin角色的Claim值</summary>
    public const string AdminRoleClaim = "OA_ADMIN";

    public bool IsAdmin(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        return user.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == AdminRoleClaim);
    }

    // 全局判定 admin：SysUserRole 非 IOrgScoped，Set<> 不带组织过滤，与原 raw SQL 同口径。
    // 用 AnyAsync(→EXISTS) 而非 SqlQueryRaw+First，避免 EF[10103]（First 无 OrderBy）噪音警告；勿改回 raw SQL。
    public async Task<bool> IsAdminByUserIdAsync(STOTOPDbContext db, long userId)
        => await db.Set<SysUserRole>()
            .AnyAsync(ur => ur.FUserId == userId && ur.FRoleId == AdminRoleId);

    public async Task<bool> IsPlatformAdminByUserIdAsync(STOTOPDbContext db, long userId)
    {
        // SYS用户 非 ITenantScoped（无租户过滤器）→ LINQ 直查安全；provider-agnostic 可 InMemory 测。
        return await db.Set<STOTOP.Module.System.Entities.SysUser>()
            .AnyAsync(u => u.FID == userId && u.FIsPlatformAdmin);
    }
}
