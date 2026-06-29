using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.PPV.Jobs;
using STOTOP.Module.PPV.Services;

namespace STOTOP.Module.PPV;

public static class PpvModuleExtensions
{
    public static IServiceCollection AddPpvModule(this IServiceCollection services)
    {
        services.AddScoped<IPpvTemplateService, PpvTemplateService>();
        services.AddScoped<IPpvRecordService, PpvRecordService>();
        services.AddScoped<IPpvResultService, PpvResultService>();
        services.AddScoped<PpvCalcJob>();

        // Event Handlers
        services.AddScoped<IEventHandler<EmployeeOrgChangedEvent>, EventHandlers.EmployeeOrgChangedHandler>();

        return services;
    }
}
