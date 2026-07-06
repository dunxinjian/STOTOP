using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

/// <inheritdoc cref="ITenantAdminScopeAccessor"/>
public sealed class TenantAdminScopeAccessor : ITenantAdminScopeAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly STOTOPDbContext _context;
    private TenantDataScope? _cached;

    public TenantAdminScopeAccessor(IHttpContextAccessor httpContextAccessor, STOTOPDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public async Task<TenantDataScope> ResolveAsync()
    {
        if (_cached != null) return _cached;

        var user = _httpContextAccessor.HttpContext?.User;

        // 平台级 admin：OA_ADMIN → 跨租户不受限（MDSTO 现状）。
        if (user?.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == AdminAuthorizationService.AdminRoleClaim) == true)
            return _cached = new TenantDataScope(true, Array.Empty<long>());

        // 登录时按身份写入的 scopeTenantId claim（租户 admin=其管辖租户；普通用户=已接受成员）。
        var ids = user?.Claims
            .Where(c => c.Type == "scopeTenantId")
            .Select(c => long.TryParse(c.Value, out var v) ? v : 0L)
            .Where(v => v != 0)
            .Distinct()
            .ToArray() ?? Array.Empty<long>();
        if (ids.Length > 0) return _cached = new TenantDataScope(false, ids);

        // 兜底：claim 缺失（本次上线前签发的旧 token / 引导路径）→ 查 SYS租户成员（已接受=2）。
        var userIdStr = user?.FindFirst("userId")?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdStr, out var userId))
        {
            var memberTenantIds = await _context.Set<SysTenantMember>()
                .Where(m => m.FUserId == userId && m.FInviteStatus == 2)
                .Select(m => m.FTenantId)
                .Distinct()
                .ToArrayAsync();
            return _cached = new TenantDataScope(false, memberTenantIds);
        }

        return _cached = new TenantDataScope(false, Array.Empty<long>());
    }
}
