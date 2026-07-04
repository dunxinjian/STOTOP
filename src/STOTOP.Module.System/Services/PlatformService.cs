using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

/// <summary>
/// <see cref="IPlatformService"/> 默认实现。直接经 <see cref="STOTOPDbContext"/> 读写 PLT 三表（平台层实体、无租户过滤器）。
/// 由 <see cref="Filters.PlatformOnlyAttribute"/> 保证仅平台超管在平台作用域下调用。
/// </summary>
public class PlatformService : IPlatformService
{
    private readonly STOTOPDbContext _db;

    public PlatformService(STOTOPDbContext db) => _db = db;

    private static string StatusName(int status) => status switch
    {
        (int)PltTenantStatus.Trial => "试用",
        (int)PltTenantStatus.Active => "正式",
        (int)PltTenantStatus.Disabled => "停用",
        (int)PltTenantStatus.Frozen => "欠费冻结",
        _ => "未知",
    };

    // ---- 租户 ----

    public async Task<List<PlatformTenantDto>> GetTenantsAsync()
    {
        var rows = await _db.Set<PltTenant>().AsNoTracking().OrderBy(t => t.FID).ToListAsync();
        return rows.Select(MapTenant).ToList();
    }

    public async Task<PlatformTenantDto?> GetTenantAsync(long id)
    {
        var t = await _db.Set<PltTenant>().AsNoTracking().FirstOrDefaultAsync(x => x.FID == id);
        return t == null ? null : MapTenant(t);
    }

    public async Task<long> CreateTenantAsync(CreatePlatformTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("租户编号不能为空");
        if (await _db.Set<PltTenant>().AnyAsync(t => t.FCode == request.Code))
            throw new InvalidOperationException($"租户编号已存在：{request.Code}");

        var tenant = new PltTenant
        {
            FName = request.Name,
            FCode = request.Code,
            FRootOrgId = request.RootOrgId,
            FAccountSetBindMode = request.AccountSetBindMode,
            FDefaultTodoChannel = request.DefaultTodoChannel,
            FPlanId = request.PlanId,
            FActivatedAt = DateTime.Now,
            FExpireAt = request.ExpireAt,
            FStatus = (int)PltTenantStatus.Trial,
        };
        _db.Set<PltTenant>().Add(tenant);
        await _db.SaveChangesAsync();
        return tenant.FID;
    }

    public async Task UpdateTenantStatusAsync(long id, int status)
    {
        if (status < (int)PltTenantStatus.Trial || status > (int)PltTenantStatus.Frozen)
            throw new InvalidOperationException($"非法租户状态：{status}");

        var tenant = await _db.Set<PltTenant>().FirstOrDefaultAsync(t => t.FID == id)
            ?? throw new InvalidOperationException($"租户不存在：{id}");
        tenant.FStatus = status;
        tenant.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    // ---- 套餐 ----

    public async Task<List<PlatformPlanDto>> GetPlansAsync()
    {
        var rows = await _db.Set<PltPlan>().AsNoTracking().OrderBy(p => p.FID).ToListAsync();
        return rows.Select(p => new PlatformPlanDto
        {
            Id = p.FID, Name = p.FName, Code = p.FCode,
            MaxUsers = p.FMaxUsers, MaxOutlets = p.FMaxOutlets,
            ModuleFlags = p.FModuleFlags, Status = p.FStatus,
        }).ToList();
    }

    public async Task<long> CreatePlanAsync(SavePlatformPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("套餐编号不能为空");
        if (await _db.Set<PltPlan>().AnyAsync(p => p.FCode == request.Code))
            throw new InvalidOperationException($"套餐编号已存在：{request.Code}");

        var plan = new PltPlan
        {
            FName = request.Name, FCode = request.Code,
            FMaxUsers = request.MaxUsers, FMaxOutlets = request.MaxOutlets,
            FModuleFlags = request.ModuleFlags,
        };
        _db.Set<PltPlan>().Add(plan);
        await _db.SaveChangesAsync();
        return plan.FID;
    }

    public async Task UpdatePlanAsync(long id, SavePlatformPlanRequest request)
    {
        var plan = await _db.Set<PltPlan>().FirstOrDefaultAsync(p => p.FID == id)
            ?? throw new InvalidOperationException($"套餐不存在：{id}");
        plan.FName = request.Name;
        plan.FMaxUsers = request.MaxUsers;
        plan.FMaxOutlets = request.MaxOutlets;
        plan.FModuleFlags = request.ModuleFlags;
        plan.FUpdateTime = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    // ---- 订阅 ----

    public async Task<List<PlatformSubscriptionDto>> GetSubscriptionsAsync(long? tenantId)
    {
        var q = _db.Set<PltSubscription>().AsNoTracking().AsQueryable();
        if (tenantId.HasValue) q = q.Where(s => s.FTenantId == tenantId.Value);
        var rows = await q.OrderByDescending(s => s.FID).ToListAsync();
        return rows.Select(s => new PlatformSubscriptionDto
        {
            Id = s.FID, TenantId = s.FTenantId, PlanId = s.FPlanId,
            PeriodStart = s.FPeriodStart, PeriodEnd = s.FPeriodEnd, Status = s.FStatus,
        }).ToList();
    }

    public async Task<long> CreateSubscriptionAsync(CreateSubscriptionRequest request)
    {
        var tenant = await _db.Set<PltTenant>().FirstOrDefaultAsync(t => t.FID == request.TenantId)
            ?? throw new InvalidOperationException($"租户不存在：{request.TenantId}");
        if (!await _db.Set<PltPlan>().AnyAsync(p => p.FID == request.PlanId))
            throw new InvalidOperationException($"套餐不存在：{request.PlanId}");
        if (request.PeriodEnd <= request.PeriodStart)
            throw new InvalidOperationException("订阅周期止必须晚于周期起");

        var sub = new PltSubscription
        {
            FTenantId = request.TenantId, FPlanId = request.PlanId,
            FPeriodStart = request.PeriodStart, FPeriodEnd = request.PeriodEnd, FStatus = 1,
        };
        _db.Set<PltSubscription>().Add(sub);

        // 订阅生效：租户置正式 + 套餐 + 开通/到期（续费=延后到期）。
        tenant.FPlanId = request.PlanId;
        tenant.FStatus = (int)PltTenantStatus.Active;
        tenant.FActivatedAt ??= request.PeriodStart;
        tenant.FExpireAt = request.PeriodEnd;
        tenant.FUpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return sub.FID;
    }

    private static PlatformTenantDto MapTenant(PltTenant t) => new()
    {
        Id = t.FID, Name = t.FName, Code = t.FCode, RootOrgId = t.FRootOrgId,
        AccountSetBindMode = t.FAccountSetBindMode, DefaultTodoChannel = t.FDefaultTodoChannel,
        PlanId = t.FPlanId, ActivatedAt = t.FActivatedAt, ExpireAt = t.FExpireAt,
        Status = t.FStatus, StatusName = StatusName(t.FStatus),
    };
}
