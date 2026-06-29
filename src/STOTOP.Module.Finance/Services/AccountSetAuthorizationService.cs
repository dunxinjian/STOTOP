using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Finance.Services;

public class AccountSetAuthorizationService : IAccountSetAuthorizationService
{
    private readonly STOTOPDbContext _context;
    private readonly ILogger<AccountSetAuthorizationService> _logger;

    public AccountSetAuthorizationService(STOTOPDbContext context, ILogger<AccountSetAuthorizationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<long>> GetUserAccountSetIdsAsync(long userId, long orgId)
    {
        // orgId 过滤已在上层 GetAllAsync 中对账套本身完成，
        // 此处仅按 userId 查询授权记录，避免 orgId 不一致导致漏匹配
        var query = _context.Set<FinAccountSetAuthorization>()
            .Where(a => a.FUserId == userId);

        // 如果 orgId > 0，优先匹配同组织的授权记录；同时包含 orgId=0 的全局授权
        if (orgId > 0)
        {
            query = query.Where(a => a.FOrgId == orgId || a.FOrgId == 0);
        }

        return await query
            .Select(a => a.FAccountSetId)
            .Distinct()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<string>> GetUserPermissionsAsync(long userId, long accountSetId)
    {
        var permissions = await (
            from auth in _context.Set<FinAccountSetAuthorization>()
            join rp in _context.Set<FinAccountSetRolePermission>()
                on auth.FAccountSetRoleId equals rp.FAccountSetRoleId
            where auth.FUserId == userId && auth.FAccountSetId == accountSetId
            select rp.FPermissionCode
        ).Distinct().ToListAsync();

        return permissions;
    }

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(long userId, long accountSetId, string permissionCode)
    {
        return await (
            from auth in _context.Set<FinAccountSetAuthorization>()
            join rp in _context.Set<FinAccountSetRolePermission>()
                on auth.FAccountSetRoleId equals rp.FAccountSetRoleId
            where auth.FUserId == userId
                && auth.FAccountSetId == accountSetId
                && rp.FPermissionCode == permissionCode
            select rp.FID
        ).AnyAsync();
    }

    /// <inheritdoc />
    public async Task<long> GrantAsync(long userId, long accountSetId, long accountSetRoleId, long orgId, long grantedBy)
    {
        // 检查是否已存在授权（唯一约束：用户+账套）
        var existing = await _context.Set<FinAccountSetAuthorization>()
            .FirstOrDefaultAsync(a => a.FUserId == userId && a.FAccountSetId == accountSetId);

        if (existing != null)
        {
            // 已存在则更新角色
            existing.FAccountSetRoleId = accountSetRoleId;
            existing.FUpdatedTime = DateTime.Now;
            await _context.SaveChangesAsync();
            return existing.FID;
        }

        var entity = new FinAccountSetAuthorization
        {
            FUserId = userId,
            FAccountSetId = accountSetId,
            FAccountSetRoleId = accountSetRoleId,
            FOrgId = orgId,
            FGrantedBy = grantedBy,
            FCreatedTime = DateTime.Now,
            FUpdatedTime = DateTime.Now
        };

        _context.Set<FinAccountSetAuthorization>().Add(entity);
        await _context.SaveChangesAsync();
        return entity.FID;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateRoleAsync(long authorizationId, long newRoleId)
    {
        var entity = await _context.Set<FinAccountSetAuthorization>()
            .FirstOrDefaultAsync(a => a.FID == authorizationId);

        if (entity == null) return false;

        entity.FAccountSetRoleId = newRoleId;
        entity.FUpdatedTime = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(long authorizationId)
    {
        var entity = await _context.Set<FinAccountSetAuthorization>()
            .FirstOrDefaultAsync(a => a.FID == authorizationId);

        if (entity == null) return false;

        _context.Set<FinAccountSetAuthorization>().Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<List<AccountSetAuthorizationDto>> GetAccountSetUsersAsync(long accountSetId)
    {
        var query =
            from auth in _context.Set<FinAccountSetAuthorization>()
            join role in _context.Set<FinAccountSetRole>()
                on auth.FAccountSetRoleId equals role.FID
            join user in _context.Set<SysUser>()
                on auth.FUserId equals user.FID
            join grantor in _context.Set<SysUser>()
                on auth.FGrantedBy equals grantor.FID into grantorJoin
            from grantor in grantorJoin.DefaultIfEmpty()
            where auth.FAccountSetId == accountSetId
            select new AccountSetAuthorizationDto
            {
                Id = auth.FID,
                UserId = auth.FUserId,
                UserName = user.FName,
                UserAccount = user.FAccount,
                AccountSetId = auth.FAccountSetId,
                AccountSetRoleId = auth.FAccountSetRoleId,
                RoleName = role.FName,
                RoleCode = role.FCode,
                OrgId = auth.FOrgId,
                GrantedBy = auth.FGrantedBy,
                GrantedByName = grantor != null ? grantor.FName : "",
                CreatedTime = auth.FCreatedTime
            };

        return await query.ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<AccountSetRoleDto>> GetRolesAsync()
    {
        var roles = await _context.Set<FinAccountSetRole>()
            .Where(r => r.FStatus == 1)
            .ToListAsync();

        var roleIds = roles.Select(r => r.FID).ToList();

        var permissions = await _context.Set<FinAccountSetRolePermission>()
            .Where(rp => roleIds.Contains(rp.FAccountSetRoleId))
            .ToListAsync();

        var permissionsByRole = permissions
            .GroupBy(p => p.FAccountSetRoleId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.FPermissionCode).ToList());

        return roles.Select(r => new AccountSetRoleDto
        {
            Id = r.FID,
            Name = r.FName,
            Code = r.FCode,
            Description = r.FDescription,
            IsSystem = r.FIsSystem,
            Permissions = permissionsByRole.GetValueOrDefault(r.FID, new List<string>())
        }).ToList();
    }
}
