using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.KSF.Events;
using STOTOP.Module.Points.EventHandlers;
using STOTOP.Module.Points.Events;
using STOTOP.Module.Points.Jobs;
using STOTOP.Module.Points.Services;
using STOTOP.Module.PPV.Events;

namespace STOTOP.Module.Points;

public static class PointsModuleExtensions
{
    /// <summary>
    /// 添加积分管理模块服务
    /// </summary>
    public static IServiceCollection AddPointsModule(this IServiceCollection services)
    {
        services.AddScoped<IPointSourceService, PointSourceService>();
        services.AddScoped<IPointRuleService, PointRuleService>();
        services.AddScoped<IPointService, PointService>();
        services.AddScoped<IPointApplicationService, PointApplicationService>();
        services.AddScoped<IRankingService, RankingService>();
        services.AddScoped<IRedeemService, RedeemService>();
        services.AddScoped<IManagerQuotaService, ManagerQuotaService>();

        // Event Handlers
        services.AddScoped<IEventHandler<PointApplicationSubmittedEvent>, PointApplicationSubmittedEventHandler>();
        services.AddScoped<IEventHandler<PointApplicationApprovedEvent>, PointApplicationApprovedEventHandler>();
        services.AddScoped<IEventHandler<PointApplicationRejectedEvent>, PointApplicationRejectedEventHandler>();
        // 跨模块联动：KSF 月度核算结果 → 积分奖扣分
        services.AddScoped<IEventHandler<KsfMonthlyResultEvent>, KsfMonthlyResultEventHandler>();
        // 跨模块联动：PPV 月度汇总完成 → 积分奖扣分
        services.AddScoped<IEventHandler<PpvMonthlyAggregatedEvent>, PpvMonthlyAggregatedEventHandler>();

        // Hangfire Jobs（清算）
        services.AddScoped<PointMonthResetJob>();
        services.AddScoped<PointYearResetJob>();

        return services;
    }
}
