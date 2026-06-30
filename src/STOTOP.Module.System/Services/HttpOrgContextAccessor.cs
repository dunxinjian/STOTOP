using Microsoft.AspNetCore.Http;
using STOTOP.Core.Services;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 从 HttpContext 获取当前组织ID（由 OrgContextMiddleware 设置）。
/// 支持显式设置（用于 Hangfire Job / BatchContextScope 等无 HttpContext 场景）。
/// </summary>
public class HttpOrgContextAccessor : IOrgContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private long? _overrideOrgId;
    private bool _hasOverride;
    private long? _overrideTenantId;
    private bool _hasTenantOverride;

    public HttpOrgContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? CurrentOrgId
    {
        get
        {
            // 显式设置的值优先（BatchContextScope 场景）
            if (_hasOverride)
                return _overrideOrgId;

            var item = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
            if (item is long orgId)
                return orgId;
            return null;
        }
        set
        {
            _overrideOrgId = value;
            _hasOverride = true;
        }
    }

    /// <summary>v2 多租户：当前【租户=客户】id。由 OrgContextMiddleware 写入 Items["CurrentTenantId"]，或非HTTP场景经 setter 显式设置。</summary>
    public long? CurrentTenantId
    {
        get
        {
            if (_hasTenantOverride)
                return _overrideTenantId;

            var item = _httpContextAccessor.HttpContext?.Items["CurrentTenantId"];
            if (item is long tenantId)
                return tenantId;
            return null;
        }
        set
        {
            _overrideTenantId = value;
            _hasTenantOverride = true;
        }
    }

    /// <summary>平台/批量受控作用域：跳过租户硬墙（非HTTP/平台场景显式置位）。</summary>
    public bool IsPlatformScope { get; set; }

    /// <summary>
    /// 清除显式设置，回退到从 HttpContext 读取
    /// </summary>
    public void ClearOverride()
    {
        _hasOverride = false;
        _overrideOrgId = null;
        _hasTenantOverride = false;
        _overrideTenantId = null;
        IsPlatformScope = false;
    }
}
