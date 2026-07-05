using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

/// <summary>
/// <see cref="IDingTalkTenantConfigService"/> 默认实现。
/// 根租户：沿用 <c>dingtalk-config.json</c> 全局配置为权威（controller/告警/cron 等既有读取路径不变，零回归）。
/// 非根租户：读写 <see cref="SysDingTalkConfig"/>（SYS钉钉配置，ITenantScoped）。
/// </summary>
public sealed class DingTalkTenantConfigService : IDingTalkTenantConfigService
{
    private readonly STOTOPDbContext _context;
    private readonly ITenantResolver _tenantResolver;
    private readonly IOrgContextAccessor _orgContext;
    private readonly IPlatformScopeFactory _platformScope;

    public DingTalkTenantConfigService(
        STOTOPDbContext context,
        ITenantResolver tenantResolver,
        IOrgContextAccessor orgContext,
        IPlatformScopeFactory platformScope)
    {
        _context = context;
        _tenantResolver = tenantResolver;
        _orgContext = orgContext;
        _platformScope = platformScope;
    }

    public Task<DingTalkConfigRecord?> GetForCurrentTenantAsync()
    {
        var tid = _orgContext.CurrentTenantId ?? _tenantResolver.GetRootTenantId();
        return tid.HasValue ? GetForTenantAsync(tid.Value) : Task.FromResult<DingTalkConfigRecord?>(null);
    }

    public async Task<DingTalkConfigRecord?> GetForTenantAsync(long tenantId)
    {
        var rootId = _tenantResolver.GetRootTenantId();
        // 根租户：JSON 全局配置为权威（向后兼容）。
        if (rootId.HasValue && tenantId == rootId.Value)
            return DingTalkConfigHelper.GetGlobalConfig();

        // 非根租户：读 SYS钉钉配置 表。IgnoreQueryFilters + 显式 F租户ID 过滤——读操作与调用方当前上下文解耦、可靠。
        var row = await _context.Set<SysDingTalkConfig>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.FTenantId == tenantId);
        return row == null ? null : MapToRecord(row);
    }

    public async Task UpsertForTenantAsync(long tenantId, DingTalkConfigRecord config)
    {
        var rootId = _tenantResolver.GetRootTenantId();
        if (rootId.HasValue && tenantId == rootId.Value)
        {
            // 根租户：写 JSON 全局配置（与既有 controller 读取路径保持一致）。
            config.OrgId = null;
            DingTalkConfigHelper.SaveConfig(config);
            return;
        }

        // 非根租户：写 SYS钉钉配置 表。跨租户写 → 平台作用域放行 fail-closed 硬墙 + 显式设 F租户ID。
        using (_platformScope.Enter("dingtalk-tenant-config-upsert"))
        {
            var row = await _context.Set<SysDingTalkConfig>()
                .AsTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.FTenantId == tenantId);
            if (row == null)
            {
                row = new SysDingTalkConfig { FTenantId = tenantId, FCreateTime = DateTime.Now };
                _context.Add(row);
            }
            row.FConfigName = config.ConfigName;
            row.FCorpId = config.CorpId;
            row.FAppKey = config.AppKey;
            row.FAppSecret = config.AppSecret;
            row.FAgentId = config.AgentId;
            row.FDomain = config.Domain;
            row.FRobotWebhookUrl = config.RobotWebhookUrl;
            row.FRobotSecret = config.RobotSecret;
            row.FIsEnabled = config.IsEnabled;
            row.FAutoSync = config.AutoSync;
            row.FSyncCron = config.SyncCron;
            row.FUpdateTime = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }

    public async Task TouchLastSyncForCurrentTenantAsync()
    {
        var tid = _orgContext.CurrentTenantId ?? _tenantResolver.GetRootTenantId();
        if (!tid.HasValue) return;

        var rootId = _tenantResolver.GetRootTenantId();
        if (rootId.HasValue && tid.Value == rootId.Value)
        {
            var g = DingTalkConfigHelper.GetGlobalConfig();
            if (g != null) DingTalkConfigHelper.UpdateLastSyncTime(g.Id);
            return;
        }

        using (_platformScope.Enter("dingtalk-touch-lastsync"))
        {
            var row = await _context.Set<SysDingTalkConfig>()
                .AsTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.FTenantId == tid.Value);
            if (row != null)
            {
                row.FLastSyncTime = DateTime.Now;
                row.FUpdateTime = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }
    }

    private static DingTalkConfigRecord MapToRecord(SysDingTalkConfig e) => new()
    {
        Id = e.FID,
        OrgId = null,
        ConfigName = e.FConfigName,
        AppKey = e.FAppKey,
        AppSecret = e.FAppSecret,
        CorpId = e.FCorpId,
        AgentId = e.FAgentId,
        Domain = e.FDomain,
        RobotWebhookUrl = e.FRobotWebhookUrl,
        RobotSecret = e.FRobotSecret,
        IsEnabled = e.FIsEnabled,
        AutoSync = e.FAutoSync,
        SyncCron = e.FSyncCron,
        LastSyncTime = e.FLastSyncTime,
        CreatedTime = e.FCreateTime,
        UpdatedTime = e.FUpdateTime,
    };
}
