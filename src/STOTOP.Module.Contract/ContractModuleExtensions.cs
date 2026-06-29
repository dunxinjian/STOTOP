using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Contract.EventHandlers;
using STOTOP.Module.Contract.Events;
using STOTOP.Module.Contract.Services;
using STOTOP.Module.Contract.Jobs;
using STOTOP.Module.Contract.Services.Interfaces;

namespace STOTOP.Module.Contract;

public static class ContractModuleExtensions
{
    /// <summary>
    /// 添加合同模块服务
    /// </summary>
    public static IServiceCollection AddContractModule(this IServiceCollection services)
    {
        services.AddScoped<IContractTypeService, ContractTypeService>();
        services.AddScoped<IContractTemplateService, ContractTemplateService>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IContractReminderService, ContractReminderService>();
        services.AddScoped<IESignService, ESignService>();
        services.AddScoped<IESignProvider, ManualSignProvider>();

        // Hangfire Job
        services.AddScoped<ContractExpiryReminderJob>();

        // Event Handlers
        services.AddScoped<IEventHandler<ContractExpiringEvent>, ContractExpiringEventHandler>();
        services.AddScoped<IEventHandler<ContractSignedEvent>, ContractSignedEventHandler>();
        services.AddScoped<IEventHandler<ContractExpiredEvent>, ContractExpiredEventHandler>();

        return services;
    }
}
