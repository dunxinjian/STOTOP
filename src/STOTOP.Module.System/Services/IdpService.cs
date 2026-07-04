using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

/// <summary>
/// <see cref="IIdpService"/> 默认实现。IDP外部企业/用户身份 是平台层表（无租户过滤器）；
/// IDP企业租户映射 是 ITenantScoped（LinkCorpToTenant 须在平台作用域或目标租户上下文下调用）。
/// </summary>
public class IdpService : IIdpService
{
    private readonly STOTOPDbContext _db;
    private readonly IOrgContextService _orgContext;
    private readonly IScopeGrantService _scopeGrant;

    public IdpService(STOTOPDbContext db, IOrgContextService orgContext, IScopeGrantService scopeGrant)
    {
        _db = db;
        _orgContext = orgContext;
        _scopeGrant = scopeGrant;
    }

    // ---- 外部企业 / 身份 ----

    public async Task<long> EnsureExternalCorpAsync(IdpProvider provider, string corpId, string name, string? accessConfig = null)
    {
        if (string.IsNullOrWhiteSpace(corpId))
            throw new InvalidOperationException("CorpId 不能为空");

        var corp = await _db.Set<IdpExternalCorp>().FirstOrDefaultAsync(c => c.FCorpId == corpId);
        if (corp == null)
        {
            corp = new IdpExternalCorp { FProvider = (int)provider, FCorpId = corpId, FName = name, FAccessConfig = accessConfig };
            _db.Set<IdpExternalCorp>().Add(corp);
        }
        else
        {
            corp.FProvider = (int)provider;
            corp.FName = name;
            if (accessConfig != null) corp.FAccessConfig = accessConfig;
            corp.FUpdateTime = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return corp.FID;
    }

    public async Task<List<IdpExternalCorpDto>> GetExternalCorpsAsync()
    {
        return await _db.Set<IdpExternalCorp>().AsNoTracking().OrderBy(c => c.FID)
            .Select(c => new IdpExternalCorpDto { Id = c.FID, Provider = c.FProvider, CorpId = c.FCorpId, Name = c.FName, Status = c.FStatus })
            .ToListAsync();
    }

    public async Task UpsertUserIdentityAsync(long userId, string corpId, string externalUserId, string? unionId)
    {
        if (string.IsNullOrWhiteSpace(corpId) || string.IsNullOrWhiteSpace(externalUserId)) return;

        var id = await _db.Set<IdpUserIdentity>().FirstOrDefaultAsync(i => i.FUserId == userId && i.FExternalCorpId == corpId);
        if (id == null)
        {
            _db.Set<IdpUserIdentity>().Add(new IdpUserIdentity
            {
                FUserId = userId, FExternalCorpId = corpId, FExternalUserId = externalUserId,
                FUnionId = unionId, FBindStatus = (int)IdpBindStatus.Bound,
            });
        }
        else
        {
            id.FExternalUserId = externalUserId;
            if (!string.IsNullOrEmpty(unionId)) id.FUnionId = unionId;
            id.FBindStatus = (int)IdpBindStatus.Bound;
            id.FUpdateTime = DateTime.Now;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<long?> ResolveUserByExternalAsync(string corpId, string externalUserId)
    {
        return await _db.Set<IdpUserIdentity>().AsNoTracking()
            .Where(i => i.FExternalCorpId == corpId && i.FExternalUserId == externalUserId && i.FBindStatus == (int)IdpBindStatus.Bound)
            .Select(i => (long?)i.FUserId)
            .FirstOrDefaultAsync();
    }

    public async Task LinkCorpToTenantAsync(string corpId, long tenantId)
    {
        if (await _db.Set<IdpTenantCorpMap>().AnyAsync(m => m.FExternalCorpId == corpId && m.FTenantId == tenantId))
            return; // 幂等
        _db.Set<IdpTenantCorpMap>().Add(new IdpTenantCorpMap { FExternalCorpId = corpId, FTenantId = tenantId });
        await _db.SaveChangesAsync();
    }

    // ---- 免登多租户消歧 ----

    public async Task<LoginTenantResolution> ResolveLoginTenantAsync(long userId)
    {
        var tenants = await _orgContext.GetMyTenantsAsync(userId); // 已接受成员
        if (tenants.Count == 0)
            return new LoginTenantResolution { Tenants = tenants };
        if (tenants.Count == 1)
            return new LoginTenantResolution { Tenants = tenants, AutoTenantId = tenants[0].TenantId };

        var primary = tenants.FirstOrDefault(t => t.IsPrimary);
        return primary != null
            ? new LoginTenantResolution { Tenants = tenants, AutoTenantId = primary.TenantId }
            : new LoginTenantResolution { Tenants = tenants, MustSelect = true }; // 428
    }

    // ---- 成员邀请 ----

    public async Task InviteMemberAsync(long inviterUserId, long targetUserId, long tenantId, bool isPrimary)
    {
        var existing = await _db.Set<SysTenantMember>()
            .FirstOrDefaultAsync(m => m.FUserId == targetUserId && m.FTenantId == tenantId);
        if (existing != null)
        {
            if (existing.FInviteStatus == 2)
                throw new InvalidOperationException("该用户已是租户成员");
            existing.FInviteStatus = 1; // 重新置待确认
            existing.FInvitedBy = inviterUserId;
            existing.FIsPrimary = isPrimary;
            existing.FStatus = 1;
            existing.FUpdateTime = DateTime.Now;
        }
        else
        {
            _db.Set<SysTenantMember>().Add(new SysTenantMember
            {
                FUserId = targetUserId, FTenantId = tenantId, FIsPrimary = isPrimary,
                FInviteStatus = 1, FInvitedBy = inviterUserId, FStatus = 1,
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task AcceptInviteAsync(long userId, long tenantId)
    {
        var m = await _db.Set<SysTenantMember>().FirstOrDefaultAsync(x => x.FUserId == userId && x.FTenantId == tenantId)
            ?? throw new InvalidOperationException("邀请不存在");
        if (m.FInviteStatus == 3)
            throw new InvalidOperationException("邀请已拒绝，无法接受");
        m.FInviteStatus = 2;
        m.FJoinedAt ??= DateTime.Now;
        m.FStatus = 1;
        m.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();

        // 接受后重算该用户在此租户的 R8 派生授权（新成员 → 可视范围就位）。best-effort。
        // 【终审修·多客户】单客户下 tenantId==当前请求租户(根)，派生正常落库。多客户下若 tenantId≠当前请求租户，
        // 派生写 SysScopeGrant(F租户ID=tenantId) 会撞跨租户写硬墙抛错——proper 修待 TenantResolver 多客户改造
        // (届时接受后须切到【被接受租户】上下文再重算 R8)。此处至少剔除挂起的 SysScopeGrant，防污染共享 ChangeTracker
        // 反噬后续 SaveChanges（照 M3 DetachPendingMembershipEntities 同款教训）。
        try
        {
            await _scopeGrant.RecomputeScopeGrantsAsync(userId, tenantId);
        }
        catch
        {
            foreach (var e in _db.ChangeTracker.Entries<SysScopeGrant>()
                         .Where(x => x.State == EntityState.Added || x.State == EntityState.Modified).ToList())
                e.State = EntityState.Detached;
        }
    }

    public async Task RejectInviteAsync(long userId, long tenantId)
    {
        var m = await _db.Set<SysTenantMember>().FirstOrDefaultAsync(x => x.FUserId == userId && x.FTenantId == tenantId)
            ?? throw new InvalidOperationException("邀请不存在");
        m.FInviteStatus = 3;
        m.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task<List<TenantInviteDto>> GetPendingInvitesAsync(long userId)
    {
        return await _db.Set<SysTenantMember>().AsNoTracking()
            .Where(m => m.FUserId == userId && m.FInviteStatus == 1)
            .Join(_db.Set<SysOrganization>(), m => m.FTenantId, o => o.FID, (m, o) => new TenantInviteDto
            {
                TenantId = m.FTenantId,
                TenantName = o.FName,
                InvitedBy = m.FInvitedBy,
                CreatedAt = m.FCreateTime,
            })
            .ToListAsync();
    }
}
