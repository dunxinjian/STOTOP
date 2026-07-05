using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Contracts.Hr;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.System.EventHandlers;
using STOTOP.Module.System.Events;
using STOTOP.Module.System.Services;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System;

public static class SystemModuleExtensions
{
    public static IServiceCollection AddSystemModule(this IServiceCollection services)
    {
        // 注册服务
        services.AddScoped<IAdminAuthorizationService, AdminAuthorizationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IDatabaseService, DatabaseService>();
        services.AddScoped<IDbConnectionService, DbConnectionService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IChangeLogService, ChangeLogService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IOrgContextService, OrgContextService>();
        services.AddScoped<IScopeGrantService, ScopeGrantService>();
        services.AddScoped<IDingTalkService, DingTalkService>();
        // 钉钉 per-tenant 配置解析（阶段4·钉钉地基：根租户=JSON全局 / 非根=SYS钉钉配置表）
        services.AddScoped<IDingTalkTenantConfigService, DingTalkTenantConfigService>();

        // 平台层服务（阶段4B：PLT租户/套餐/订阅 跨租户管理，供 /api/platform/* 平台超管消费）
        services.AddScoped<IPlatformService, PlatformService>();

        // 外部身份服务（阶段4D·M8：IDP 外部企业/用户身份/免登多租户消歧/成员邀请）
        services.AddScoped<IIdpService, IdpService>();

        // 租户默认待办渠道解析（阶段4E·D3：闭合 4A 的 PLT租户.FDefaultTodoChannel，供 CardFlow 派发消费）
        services.AddScoped<STOTOP.Core.Services.ITenantTodoChannelResolver, TenantTodoChannelResolver>();

        // 安全与会话管理服务
        services.AddScoped<SecurityConfigService>();
        services.AddScoped<SecurityAuditService>();
        // 审计写入接口 → 复用同一 SecurityAuditService 实例（供平台旁路审计以抽象依赖、可测）。
        services.AddScoped<ISecurityAuditService>(sp => sp.GetRequiredService<SecurityAuditService>());
        services.AddScoped<SessionService>();

        services.AddScoped<IThemeSettingService, ThemeSettingService>();
        services.AddScoped<IEnterpriseInfoService, EnterpriseInfoService>();
        services.AddHttpClient();

        // 系统告警服务
        services.AddScoped<ISystemAlertService, SystemAlertService>();

        // 编码规则服务
        services.AddScoped<ICodeRuleService, CodeRuleService>();

        // 组织类型服务
        services.AddScoped<IOrgTypeService, OrgTypeService>();

        // 组织账套解析服务
        services.AddScoped<IOrgAccountSetResolver, OrgAccountSetResolver>();

        // 员工组织/岗位查询服务（供 KSF/PPV/Points 等模块消费）
        services.AddScoped<IEmployeeOrgQueryService, EmployeeOrgQueryService>();

        // Schema 同步管理服务
        services.AddScoped<ISchemaSyncManageService, SchemaSyncManageService>();
        services.AddScoped<IFeedbackService, FeedbackService>();

        // 事件处理器
        services.AddScoped<IEventHandler<DingTalkSyncCompletedEvent>, DingTalkSyncCompletedEventHandler>();
        services.AddScoped<IEventHandler<SystemAlertEvent>, SystemAlertEventHandler>();

        return services;
    }
}
