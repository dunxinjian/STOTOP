using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Quality.EventHandlers;
using STOTOP.Module.Quality.Events;
using STOTOP.Module.Quality.Services.Dashboard;
using STOTOP.Module.Quality.Services.Detection;
using STOTOP.Module.Quality.Services.Dispatch;
using STOTOP.Module.Quality.Services.Exception;
using STOTOP.Module.Quality.Services.Knowledge;
using STOTOP.Module.Quality.Services.Performance;
using STOTOP.Module.Quality.Services.Review;
using STOTOP.Module.Quality.Services.Alert;
using STOTOP.Module.Quality.Services.Rule;
using STOTOP.Module.Quality.Services.CarrierDashboard;
using STOTOP.Module.Quality.Services.Unification;

namespace STOTOP.Module.Quality;

public static class QualityModuleExtensions
{
    /// <summary>
    /// 添加质量中心模块服务
    /// </summary>
    public static IServiceCollection AddQualityModule(this IServiceCollection services)
    {
        services.AddScoped<IQualityDashboardService, QualityDashboardService>();
        services.AddScoped<ICarrierQualityDashboardService, CarrierQualityDashboardService>();
        services.AddScoped<IExceptionService, ExceptionService>();
        services.AddScoped<IQualityRuleService, QualityRuleService>();
        services.AddScoped<IDetectionService, DetectionService>();
        services.AddScoped<IDispatchService, DispatchService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IKnowledgeService, KnowledgeService>();
        services.AddScoped<IPerformanceService, PerformanceService>();
        services.AddScoped<IAlertConfigService, AlertConfigService>();

        // 统一质控：主数据匹配 + 归一服务
        services.AddScoped<IMasterDataMatcher, MasterDataMatcher>();
        services.AddScoped<IQualityUnificationService, QualityUnificationService>();

        // 事件处理器
        services.AddScoped<IEventHandler<ExceptionCreatedEvent>, ExceptionCreatedEventHandler>();
        services.AddScoped<IEventHandler<ExceptionDispatchedEvent>, ExceptionDispatchedEventHandler>();
        services.AddScoped<IEventHandler<ExceptionClosedEvent>, ExceptionClosedEventHandler>();

        return services;
    }
}
