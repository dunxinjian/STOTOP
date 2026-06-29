using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.KSF.EventHandlers;
using STOTOP.Module.KSF.Jobs;
using STOTOP.Module.KSF.Services;

namespace STOTOP.Module.KSF;

public static class KsfModuleExtensions
{
    /// <summary>
    /// 添加 KSF 模块服务
    /// </summary>
    public static IServiceCollection AddKsfModule(this IServiceCollection services)
    {
        services.AddScoped<IKsfIndicatorService, KsfIndicatorService>();
        services.AddScoped<IKsfPlanService, KsfPlanService>();
        services.AddScoped<IKsfResultService, KsfResultService>();
        services.AddScoped<IKsfMappingService, KsfMappingService>();

        // KSF 月度核算 Job（由 Hangfire 在 Program.cs 中按 RecurringJob 触发）
        services.AddScoped<KsfCalcJob>();

        // Event Handlers：订阅员工组织变更事件，提示运维同步调整 KSF 映射
        services.AddScoped<IEventHandler<EmployeeOrgChangedEvent>, EmployeeOrgChangedHandler>();
        return services;
    }
}
