using Microsoft.Extensions.DependencyInjection;
using STOTOP.Module.HR.Services;

namespace STOTOP.Module.HR;

public static class HrModuleExtensions
{
    /// <summary>
    /// 添加人力资源模块服务
    /// </summary>
    public static IServiceCollection AddHrModule(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeService, EmployeeService>();
        return services;
    }
}
