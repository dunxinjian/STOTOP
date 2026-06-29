using Microsoft.Extensions.DependencyInjection;
using STOTOP.Module.Dormitory.Services;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory;

public static class DormitoryModuleExtensions
{
    /// <summary>
    /// 添加宿舍管理模块服务
    /// </summary>
    public static IServiceCollection AddDormitoryModule(this IServiceCollection services)
    {
        // 注册核心服务
        services.AddScoped<IBuildingService, BuildingService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IBedService, BedService>();

        // 注册业务服务
        services.AddScoped<IResidenceService, ResidenceService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IFacilityService, FacilityService>();
        services.AddScoped<IRepairOrderService, RepairOrderService>();
        services.AddScoped<IVisitorService, VisitorService>();
        services.AddScoped<IHygieneCheckService, HygieneCheckService>();
        services.AddScoped<IStatisticsService, StatisticsService>();

        return services;
    }
}
