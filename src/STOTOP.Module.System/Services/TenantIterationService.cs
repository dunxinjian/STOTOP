using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Services;

/// <summary>
/// <see cref="ITenantIterationService"/> 的默认实现：枚举活跃 PLT租户，逐个经 <see cref="ITenantScopeFactory"/>
/// 固化租户上下文后运行 action。详见接口注释（向后兼容 / 隔离 / 冻结照跑）。
/// </summary>
public sealed class TenantIterationService : ITenantIterationService
{
    private readonly ITenantScopeFactory _scopeFactory;
    private readonly IServiceScopeFactory _diScopeFactory;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<TenantIterationService> _logger;

    public TenantIterationService(
        ITenantScopeFactory scopeFactory,
        IServiceScopeFactory diScopeFactory,
        ITenantResolver tenantResolver,
        ILogger<TenantIterationService> logger)
    {
        _scopeFactory = scopeFactory;
        _diScopeFactory = diScopeFactory;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    public async Task ForEachActiveTenantAsync(Func<long, Task> action, string reason = "tenant-iteration")
    {
        ArgumentNullException.ThrowIfNull(action);

        var tenantIds = ResolveActiveTenantIds();
        if (tenantIds.Count == 0)
        {
            _logger.LogWarning("per-tenant 迭代[{Reason}]：未解析到任何活跃租户，跳过。", reason);
            return;
        }

        var success = 0;
        var failed = 0;
        foreach (var tid in tenantIds)
        {
            try
            {
                using (_scopeFactory.Enter(tid, reason))
                {
                    await action(tid);
                }
                success++;
            }
            catch (Exception ex)
            {
                // 单租户失败隔离：不中断其它租户（照 ShentongUnificationJob 的 per-org 隔离范式）。
                failed++;
                _logger.LogError(ex, "per-tenant 迭代[{Reason}]：租户 {TenantId} 处理失败（已隔离，继续其它租户）", reason, tid);
            }
        }

        // 多租户或有失败时记一条汇总，便于运维核对覆盖面（单客户单成功不噪声）。
        if (tenantIds.Count > 1 || failed > 0)
            _logger.LogInformation("per-tenant 迭代[{Reason}] 完成：活跃租户 {Total}，成功 {Success}，失败 {Failed}。",
                reason, tenantIds.Count, success, failed);
    }

    /// <summary>
    /// 枚举活跃租户 FID：含 试用(1)/正式(2)/欠费冻结(4)，排除 停用(3)（D7 决策：冻结照跑）。
    /// PLT租户 表未建 / 空表 / 不可读 → 回退单租户（= <see cref="ITenantResolver.GetRootTenantId"/>），保证向后兼容、Job 不空转。
    /// </summary>
    private IReadOnlyList<long> ResolveActiveTenantIds()
    {
        try
        {
            using var scope = _diScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();
            // PltTenant 是平台层实体（不实现 ITenantScoped）→ 无租户过滤器，直接读全表。
            var ids = db.Set<PltTenant>()
                .AsNoTracking()
                .Where(t => t.FStatus != (int)PltTenantStatus.Disabled)
                .OrderBy(t => t.FID)
                .Select(t => t.FID)
                .ToList();
            if (ids.Count > 0) return ids;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "per-tenant 迭代：枚举 PLT租户 失败（表未建 / 升级窗口），回退单租户。");
        }

        var root = _tenantResolver.GetRootTenantId();
        return root.HasValue ? new List<long> { root.Value } : Array.Empty<long>();
    }
}
