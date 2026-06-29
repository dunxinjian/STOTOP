using Microsoft.Extensions.DependencyInjection;
using STOTOP.Module.Vehicle.Services;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle;

public static class VehicleModuleExtensions
{
    /// <summary>
    /// 添加车辆管理模块服务
    /// </summary>
    public static IServiceCollection AddVehicleModule(this IServiceCollection services)
    {
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IRentalStandardService, RentalStandardService>();
        services.AddScoped<IRentalChargeService, RentalChargeService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IGpsService, GpsService>();

        return services;
    }
}
