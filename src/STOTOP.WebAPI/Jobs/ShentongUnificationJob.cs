using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Module.Quality.Services.Unification;

namespace STOTOP.WebAPI.Jobs;

/// <summary>
/// 申通统一质控归一定时任务（每日执行，RecurringJobId: shentong-unify）。
///
/// 流程：
/// 1. 枚举「有申通数据可归一」的组织（事件表 ∪ 各申通 STG 源表 distinct FOrgId），只跑真正有数据的组织。
/// 2. 对每个组织<b>新建一个 DI scope</b>，在该 scope 内设 <see cref="IOrgContextAccessor.CurrentOrgId"/>（HTTP 外的组织上下文来源），
///    解析 scoped 归一服务后调 <see cref="IQualityUnificationService.UnifyShentongAsync"/> 整批归一。
/// 3. <b>单组织异常隔离</b>：try-catch 包住单组织，失败只记日志不中断其它组织。
///
/// 为何每组织独立 scope：归一服务 + STOTOPDbContext + IOrgContextAccessor 均 Scoped；逐组织独立 scope 可
///   ① 让每组织有干净的 DbContext（避免变更跟踪/连接状态跨组织串扰）；② 在 scope 内安全设置组织上下文。
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 1800)]
[AutomaticRetry(Attempts = 0)]
public class ShentongUnificationJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShentongUnificationJob> _logger;

    public ShentongUnificationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ShentongUnificationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("ShentongUnificationJob 启动");

        // ── 1. 枚举有申通数据的组织（用一个临时 scope 取归一服务跑枚举）──
        IReadOnlyList<long> orgIds;
        using (var enumScope = _scopeFactory.CreateScope())
        {
            var svc = enumScope.ServiceProvider.GetRequiredService<IQualityUnificationService>();
            orgIds = await svc.ListShentongOrgIdsAsync();
        }

        if (orgIds.Count == 0)
        {
            _logger.LogInformation("ShentongUnificationJob 无可归一的申通数据组织，结束");
            return;
        }
        _logger.LogInformation("ShentongUnificationJob 待跑组织数={Count}：[{Orgs}]", orgIds.Count, string.Join(",", orgIds));

        // ── 2. 逐组织：独立 scope + 设组织上下文 + 归一，单组织异常隔离 ──
        int ok = 0, fail = 0;
        foreach (var orgId in orgIds)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                // HTTP 外组织上下文：在 scope 内设置 IOrgContextAccessor.CurrentOrgId（归一服务全程显式带 orgId，
                // 设置上下文是为统一查询过滤器口径并与请求内行为一致）。
                var orgCtx = scope.ServiceProvider.GetRequiredService<IOrgContextAccessor>();
                orgCtx.CurrentOrgId = orgId;

                var svc = scope.ServiceProvider.GetRequiredService<IQualityUnificationService>();
                var result = await svc.UnifyShentongAsync(orgId);
                ok++;
                _logger.LogInformation(
                    "ShentongUnificationJob 组织 {OrgId} 归一完成 | 事件={Events} 员工指标={Emp} 网点指标={Net} 网点未匹配={NetUn} 员工未匹配={EmpUn}",
                    orgId, result.EventsUpserted, result.EmployeeMetricUpserts, result.NetworkMetricUpserts,
                    result.NetworkUnmatched, result.EmployeeUnmatched);
            }
            catch (Exception ex)
            {
                fail++;
                _logger.LogError(ex, "ShentongUnificationJob 组织 {OrgId} 归一失败（已隔离，继续其它组织）", orgId);
            }
        }

        _logger.LogInformation("ShentongUnificationJob 完成 | 组织={Total} 成功={Ok} 失败={Fail}", orgIds.Count, ok, fail);
    }
}
