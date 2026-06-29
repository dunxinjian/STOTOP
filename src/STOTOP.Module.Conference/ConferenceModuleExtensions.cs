using Microsoft.Extensions.DependencyInjection;
using STOTOP.Module.Conference.Services;
using STOTOP.Module.Conference.Services.Interfaces;

namespace STOTOP.Module.Conference;

public static class ConferenceModuleExtensions
{
    /// <summary>
    /// 添加会务管理模块服务
    /// </summary>
    public static IServiceCollection AddConferenceModule(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IAttendeeService, AttendeeService>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<ITransportService, TransportService>();
        services.AddScoped<IVehicleScheduleService, VehicleScheduleService>();
        services.AddScoped<IAccommodationService, AccommodationService>();
        services.AddScoped<IMealService, MealService>();
        services.AddScoped<ITableArrangementService, TableArrangementService>();
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IGiftService, GiftService>();
        services.AddScoped<ICeremonyService, CeremonyService>();
        return services;
    }
}
