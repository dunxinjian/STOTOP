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

    // 全局判定 admin：SysUserRole 非 IOrgScoped，Set<> 不带组织过滤。
    // R5：从"持 FRoleId=1"改为"持 F是否管理员=1 的角色"——含各租户私有 admin 角色（存量 role1 迁移 V15 已置 1）。
    // 仍用 AnyAsync(→EXISTS) 避免 EF[10103] 噪音；勿改回 raw SQL。
    public async Task<bool> IsAdminByUserIdAsync(STOTOPDbContext db, long userId)
        => await db.Set<SysUserRole>()
            .Where(ur => ur.FUserId == userId)
            .Join(db.Set<SysRole>().Where(r => r.FIsAdmin), ur => ur.FRoleId, r => r.FID, (ur, r) => r.FID)
            .AnyAsync();

    public async Task<bool> IsPlatformAdminByUserIdAsync(STOTOPDbContext db, long userId)
    {
        // SYS用户 非 ITenantScoped（无租户过滤器）→ LINQ 直查安全；provider-agnostic 可 InMemory 测。
        return await db.Set<STOTOP.Module.System.Entities.SysUser>()
            .AnyAsync(u => u.FID == userId && u.FIsPlatformAdmin);
    }
}
