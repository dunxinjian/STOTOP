using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Filters;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

/// <summary>
/// 平台层控制器（多租户阶段4B）。<see cref="PlatformOnlyAttribute"/> 统一门禁：仅平台超管、进入平台作用域（物理脱离租户硬墙）。
/// 路由 /api/platform/* 已在 OrgContextMiddleware.SkipPaths（不解析租户上下文）+ TenantFreezeMiddleware.SkipPaths（续费/解冻不受冻结影响）。
/// </summary>
[Route("api/platform")]
[ApiController]
[Authorize]
[PlatformOnly]
public class PlatformController : ControllerBase
{
    private readonly IPlatformService _platform;

    public PlatformController(IPlatformService platform) => _platform = platform;

    // ---- 租户 ----

    [HttpGet("tenants")]
    public async Task<ApiResult<List<PlatformTenantDto>>> GetTenants()
        => ApiResult<List<PlatformTenantDto>>.Success(await _platform.GetTenantsAsync());

    [HttpGet("tenants/{id}")]
    public async Task<ApiResult<PlatformTenantDto?>> GetTenant(long id)
    {
        var t = await _platform.GetTenantAsync(id);
        return t == null ? ApiResult<PlatformTenantDto?>.Fail("租户不存在", 404) : ApiResult<PlatformTenantDto?>.Success(t);
    }

    [HttpPost("tenants")]
    public async Task<ApiResult<long>> CreateTenant([FromBody] CreatePlatformTenantRequest request)
        => ApiResult<long>.Success(await _platform.CreateTenantAsync(request));

    /// <summary>更新租户状态（冻结/解冻/停用/正式）。冻结即触发 D7 白名单。</summary>
    [HttpPut("tenants/{id}/status")]
    public async Task<ApiResult<bool>> UpdateTenantStatus(long id, [FromBody] UpdateTenantStatusRequest request)
    {
        await _platform.UpdateTenantStatusAsync(id, request.Status);
        return ApiResult<bool>.Success(true);
    }

    // ---- 套餐 ----

    [HttpGet("plans")]
    public async Task<ApiResult<List<PlatformPlanDto>>> GetPlans()
        => ApiResult<List<PlatformPlanDto>>.Success(await _platform.GetPlansAsync());

    [HttpPost("plans")]
    public async Task<ApiResult<long>> CreatePlan([FromBody] SavePlatformPlanRequest request)
        => ApiResult<long>.Success(await _platform.CreatePlanAsync(request));

    [HttpPut("plans/{id}")]
    public async Task<ApiResult<bool>> UpdatePlan(long id, [FromBody] SavePlatformPlanRequest request)
    {
        await _platform.UpdatePlanAsync(id, request);
        return ApiResult<bool>.Success(true);
    }

    // ---- 订阅 ----

    [HttpGet("subscriptions")]
    public async Task<ApiResult<List<PlatformSubscriptionDto>>> GetSubscriptions([FromQuery] long? tenantId)
        => ApiResult<List<PlatformSubscriptionDto>>.Success(await _platform.GetSubscriptionsAsync(tenantId));

    [HttpPost("subscriptions")]
    public async Task<ApiResult<long>> CreateSubscription([FromBody] CreateSubscriptionRequest request)
        => ApiResult<long>.Success(await _platform.CreateSubscriptionAsync(request));
}
