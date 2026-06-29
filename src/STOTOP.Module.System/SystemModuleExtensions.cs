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
        services.AddScoped<IDingTalkService, DingTalkService>();

        // 安全与会话管理服务
        services.AddScoped<SecurityConfigService>();
        services.AddScoped<SecurityAuditService>();
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
