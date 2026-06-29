using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.System.Services;

public class AdminAuthorizationService : IAdminAuthorizationService
{
    /// <summary>Admin角色的数据库ID</summary>
    public const long AdminRoleId = 1;

    /// <summary>JWT中admin角色的Claim值</summary>
    public const string AdminRoleClaim = "OA_ADMIN";

    public bool IsAdmin(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        return user.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == AdminRoleClaim);
    }

    public async Task<bool> IsAdminByUserIdAsync(STOTOPDbContext db, long userId)
    {
        var count = await db.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(1) AS [Value] FROM [SYS用户角色] WHERE [F用户ID] = {0} AND [F角色ID] = {1}",
                userId, AdminRoleId)
            .FirstOrDefaultAsync();

        return count > 0;
    }
}
