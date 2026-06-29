using Microsoft.Extensions.DependencyInjection;
using STOTOP.Module.CRM.Services;
using STOTOP.Module.CRM.Services.Interfaces;

namespace STOTOP.Module.CRM;

public static class CrmModuleExtensions
{
    /// <summary>
    /// 添加CRM模块服务
    /// </summary>
    public static IServiceCollection AddCrmModule(this IServiceCollection services)
    {
        services.AddScoped<ICrmOrgService, CrmOrgService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IVisitRecordService, VisitRecordService>();
        services.AddScoped<IServiceOrderService, ServiceOrderService>();
        services.AddScoped<IServiceFeedbackService, ServiceFeedbackService>();
        services.AddScoped<IReferralCommissionService, ReferralCommissionService>();
        services.AddScoped<IPrepaymentWaybillService, PrepaymentWaybillService>();
        services.AddScoped<IProfitCalcService, ProfitCalcService>();
        services.AddScoped<IBonusService, BonusService>();
        return services;
    }
}
