using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;

namespace STOTOP.Module.System.Services;

/// <summary>
/// 过渡期租户解析：当前生产库为单客户(MDSTO)。阶段4A 起租户由平台层实体 <c>PLT租户</c> 定义（不再"恰好=组织树根"），
/// 故优先返回唯一非停用 PLT租户 的 FID；PLT租户 尚未回填时回退组织树根节点 id（F父ID=0）。
/// 单客户下二者相等（V13 以 IDENTITY_INSERT 令 PLT租户.FID=根组织id），行为不变、但语义解耦到真实租户实体。
/// 原生 SQL 查询避开组织/租户全局过滤器；首次解析后缓存。多客户上线后（阶段4C）改为按 X-Tenant-Context + 成员关系解析。
/// </summary>
public class TenantResolver : ITenantResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private long? _cached;
    private bool _resolved;
    // org → 租户 缓存：org→tenant 映射迁移后稳定；与 GetRootTenantId 同缓存哲学（变更需重启生效）。
    // 注意 global:: —— 本文件命名空间 STOTOP.Module.System.* 会遮蔽裸 System.*。
    private readonly global::System.Collections.Concurrent.ConcurrentDictionary<long, long?> _orgTenantCache = new();

    public TenantResolver(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public long? GetRootTenantId()
    {
        if (_resolved) return _cached;
        lock (_lock)
        {
            if (_resolved) return _cached;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            _cached = ResolveTenantId(db) ?? ResolveRootOrgId(db);
            _resolved = true;
            return _cached;
        }
    }

    /// <summary>
    /// 解析指定组织所属的租户 id：读 SYS组织架构.F租户ID（原生 SQL 避开过滤器）。
    /// 存量 0 / 未回填 / 查询失败 → 兜底 <see cref="GetRootTenantId"/>。按 org 缓存（映射迁移后稳定）。
    /// </summary>
    public long? ResolveTenantForOrg(long orgId)
    {
        if (orgId <= 0) return GetRootTenantId();
        return _orgTenantCache.GetOrAdd(orgId, oid =>
        {
            long resolved = 0;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
                var rows = db.Database
                    .SqlQueryRaw<long>("SELECT TOP 1 [F租户ID] AS [Value] FROM [SYS组织架构] WHERE [FID] = {0}", oid)
                    .ToList();
                resolved = rows.Count > 0 ? rows[0] : 0;
            }
            catch
            {
                // 表不可读（InMemory / 升级窗口）→ 走兜底。
                resolved = 0;
            }
            return resolved != 0 ? resolved : GetRootTenantId();
        });
    }

    /// <summary>唯一活跃(非停用=状态&lt;&gt;3)平台租户的 FID；PLT租户 表不存在或空表返回 null（回退组织树根）。</summary>
    private static long? ResolveTenantId(STOTOPDbContext db)
    {
        try
        {
            var ids = db.Database
                .SqlQueryRaw<long>("SELECT TOP 1 [FID] AS [Value] FROM [PLT租户] WHERE [F状态] <> 3 ORDER BY [FID]")
                .ToList();
            return ids.Count > 0 ? ids[0] : (long?)null;
        }
        catch
        {
            // PLT租户 表尚未由 CreateMissingTables 建立（升级窗口/InMemory）→ 回退组织树根。
            return null;
        }
    }

    private static long? ResolveRootOrgId(STOTOPDbContext db)
    {
        try
        {
            var ids = db.Database
                .SqlQueryRaw<long>("SELECT TOP 1 [FID] AS [Value] FROM [SYS组织架构] WHERE [F父ID] = 0 ORDER BY [FID]")
                .ToList();
            return ids.Count > 0 ? ids[0] : (long?)null;
        }
        catch
        {
            return null;
        }
    }
}
