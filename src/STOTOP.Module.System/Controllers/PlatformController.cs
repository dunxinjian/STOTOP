using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
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
    private readonly IIdpService _idp;
    private readonly IProvisionTenantService _provision;

    public PlatformController(IPlatformService platform, IIdpService idp, IProvisionTenantService provision)
    {
        _platform = platform;
        _idp = idp;
        _provision = provision;
    }

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

    /// <summary>开通新租户（R5）：建组织根 + 初始管理员 + 私有 admin 角色 + 成员 + R8，返回一次性初始密码。</summary>
    [HttpPost("tenants")]
    public async Task<ApiResult<ProvisionTenantResult>> CreateTenant([FromBody] ProvisionTenantRequest request)
        => ApiResult<ProvisionTenantResult>.Success(await _provision.ProvisionAsync(request));

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

    // ---- 外部身份企业（M8·平台）：本控制器已在平台作用域，IDP企业租户映射(ITenantScoped) 写入可显式落 FTenantId ----

    [HttpGet("idp/corps")]
    public async Task<ApiResult<List<IdpExternalCorpDto>>> GetExternalCorps()
        => ApiResult<List<IdpExternalCorpDto>>.Success(await _idp.GetExternalCorpsAsync());

    [HttpPost("idp/corps")]
    public async Task<ApiResult<long>> EnsureExternalCorp([FromBody] SaveExternalCorpRequest request)
        => ApiResult<long>.Success(await _idp.EnsureExternalCorpAsync((IdpProvider)request.Provider, request.CorpId, request.Name, request.AccessConfig));

    /// <summary>企业↔租户绑定（N:N；一 corp 服务多租户 / 一租户接多 corp）。</summary>
    [HttpPost("idp/link-tenant")]
    public async Task<ApiResult<bool>> LinkCorpToTenant([FromBody] LinkCorpTenantRequest request)
    {
        await _idp.LinkCorpToTenantAsync(request.CorpId, request.TenantId);
        return ApiResult<bool>.Success(true);
    }
}
