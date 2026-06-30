using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;

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
}
