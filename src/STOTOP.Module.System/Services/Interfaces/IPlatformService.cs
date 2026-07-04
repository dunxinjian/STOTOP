using STOTOP.Module.System.Dtos;

namespace STOTOP.Module.System.Services.Interfaces;

/// <summary>
/// 平台层服务（多租户阶段4B）：平台超管对 PLT租户/套餐/订阅 的跨租户管理。
/// 所有方法均在 <see cref="Filters.PlatformOnlyAttribute"/> 已进入平台作用域的请求内调用。
/// PLT 三表为平台层实体（非 ITenantScoped），本服务读写不受租户硬墙约束。
/// </summary>
public interface IPlatformService
{
    // ---- 租户 ----
    Task<List<PlatformTenantDto>> GetTenantsAsync();
    Task<PlatformTenantDto?> GetTenantAsync(long id);
    Task<long> CreateTenantAsync(CreatePlatformTenantRequest request);
    /// <summary>更新租户状态（试用/正式/停用/欠费冻结）。冻结即触发 D7 白名单（TenantFreezeMiddleware）。</summary>
    Task UpdateTenantStatusAsync(long id, int status);

    // ---- 套餐 ----
    Task<List<PlatformPlanDto>> GetPlansAsync();
    Task<long> CreatePlanAsync(SavePlatformPlanRequest request);
    Task UpdatePlanAsync(long id, SavePlatformPlanRequest request);

    // ---- 订阅 ----
    Task<List<PlatformSubscriptionDto>> GetSubscriptionsAsync(long? tenantId);
    /// <summary>创建订阅并把租户置正式、写开通/到期时间与套餐（续费同法：新周期订阅）。</summary>
    Task<long> CreateSubscriptionAsync(CreateSubscriptionRequest request);
}
