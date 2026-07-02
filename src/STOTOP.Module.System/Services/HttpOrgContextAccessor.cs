using Microsoft.AspNetCore.Http;
using STOTOP.Core.Services;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 当前组织/租户上下文访问器。
/// <para>
/// HTTP 请求：由 OrgContextMiddleware 写入 <c>HttpContext.Items</c>，经 <see cref="IHttpContextAccessor"/>（本身 AsyncLocal）
/// 跨作用域可见。非 HTTP 场景（Hangfire 任务、事件处理器/回调/插件的子 DI 作用域、后台 Task.Run 等）经 setter 显式设置——
/// 这些 override 存 <see cref="AsyncLocal{T}"/>（静态、随异步执行流传播），故在【子作用域新建的本类实例】上也能读到，
/// 保证 fail-closed 租户硬墙在跨作用域后台链路上不丢上下文。
/// </para>
/// <para>
/// 隔离性：AsyncLocal 按异步执行流（ExecutionContext）隔离——每个 HTTP 请求、每次 Hangfire 任务都是独立执行流，
/// override 只沿流向下传播、不跨请求/任务泄漏（与 <see cref="IHttpContextAccessor"/> 同机制）。HTTP 请求不设 override（用 Items）。
/// </para>
/// </summary>
public class HttpOrgContextAccessor : IOrgContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    // 静态 AsyncLocal：随异步流穿透子 DI 作用域（子作用域新建实例读同一静态值）；按执行流隔离，不跨请求/任务泄漏。
    private static readonly AsyncLocal<long?> _overrideOrgId = new();
    private static readonly AsyncLocal<bool> _hasOverrideOrg = new();
    private static readonly AsyncLocal<long?> _overrideTenantId = new();
    private static readonly AsyncLocal<bool> _hasOverrideTenant = new();
    private static readonly AsyncLocal<bool> _isPlatformScope = new();

    public HttpOrgContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? CurrentOrgId
    {
        get
        {
            // 显式设置的值优先（后台/子作用域场景）
            if (_hasOverrideOrg.Value)
                return _overrideOrgId.Value;

            var item = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
            if (item is long orgId)
                return orgId;
            return null;
        }
        set
        {
            _overrideOrgId.Value = value;
            _hasOverrideOrg.Value = true;
        }
    }

    /// <summary>v2 多租户：当前【租户=客户】id。HTTP 由中间件写 Items；非HTTP场景经 setter 显式设置（存 AsyncLocal 穿透子作用域）。</summary>
    public long? CurrentTenantId
    {
        get
        {
            if (_hasOverrideTenant.Value)
                return _overrideTenantId.Value;

            var item = _httpContextAccessor.HttpContext?.Items["CurrentTenantId"];
            if (item is long tenantId)
                return tenantId;
            return null;
        }
        set
        {
            _overrideTenantId.Value = value;
            _hasOverrideTenant.Value = true;
        }
    }

    /// <summary>平台/批量受控作用域：跳过租户硬墙（存 AsyncLocal，随异步流传播至子作用域）。</summary>
    public bool IsPlatformScope
    {
        get => _isPlatformScope.Value;
        set => _isPlatformScope.Value = value;
    }

    /// <summary>
    /// 清除当前执行流的显式设置，回退到从 HttpContext 读取。
    /// </summary>
    public void ClearOverride()
    {
        _hasOverrideOrg.Value = false;
        _overrideOrgId.Value = null;
        _hasOverrideTenant.Value = false;
        _overrideTenantId.Value = null;
        _isPlatformScope.Value = false;
    }
}
