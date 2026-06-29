using Microsoft.Extensions.DependencyInjection;
using STOTOP.Module.Supplier.Services;
using STOTOP.Module.Supplier.Services.Interfaces;

namespace STOTOP.Module.Supplier;

public static class SupplierModuleExtensions
{
    /// <summary>
    /// 添加供应商模块服务
    /// </summary>
    public static IServiceCollection AddSupplierModule(this IServiceCollection services)
    {
        services.AddScoped<ISupplierService, SupplierService>();

        return services;
    }
}
