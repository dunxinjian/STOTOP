using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 过渡期租户解析：当前生产库为单客户(MDSTO)，租户 = 组织树根节点 id（F父ID=0），首次查询后缓存。
/// 原生 SQL 查询，避开组织/租户全局过滤器。多客户上线后替换为按用户成员关系解析。
/// </summary>
public class TenantResolver : ITenantResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private long? _cached;
    private bool _resolved;

    public TenantResolver(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public long? GetRootTenantId()
    {
        if (_resolved) return _cached;
        lock (_lock)
        {
            if (_resolved) return _cached;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            var ids = db.Database
                .SqlQueryRaw<long>("SELECT TOP 1 [FID] AS [Value] FROM [SYS组织架构] WHERE [F父ID] = 0 ORDER BY [FID]")
                .ToList();
            _cached = ids.Count > 0 ? ids[0] : (long?)null;
            _resolved = true;
            return _cached;
        }
    }

    /// <inheritdoc/>
    public long? ResolveTenantForOrg(long orgId)
    {
        // 非法 orgId：无从解析 → 根租户兜底。
        if (orgId <= 0) return GetRootTenantId();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
        // SYS组织架构 不实现 IOrgScoped/ITenantScoped(是租户结构骨架、不进硬墙)，Set<> 无全局过滤器，可直接读。
        var tenantId = db.Set<SysOrganization>()
            .Where(o => o.FID == orgId)
            .Select(o => o.FTenantId)
            .FirstOrDefault();

        // F租户ID<=0：查不到该组织，或其租户根尚未由 OrgTreeMaterializer 物化(新建/reparent 的瞬时窗口、
        // 或阶段0 回填遗漏)。此时回退根租户，避免把上下文解析成 0 → fail-closed 读空/写抛/污染。
        return tenantId > 0 ? tenantId : GetRootTenantId();
    }
}
