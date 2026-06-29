using Microsoft.Extensions.DependencyInjection;
using STOTOP.Module.Salary.Jobs;
using STOTOP.Module.Salary.Services;

namespace STOTOP.Module.Salary;

public static class SalaryModuleExtensions
{
    /// <summary>
    /// 添加 Salary 模块服务
    /// </summary>
    public static IServiceCollection AddSalaryModule(this IServiceCollection services)
    {
        services.AddScoped<ISalaryGradeService, SalaryGradeService>();
        services.AddScoped<ISalaryArchiveService, SalaryArchiveService>();
        services.AddScoped<ISalaryPayrollService, SalaryPayrollService>();
        services.AddScoped<IPromotionRuleService, PromotionRuleService>();
        services.AddScoped<IPromotionReviewService, PromotionReviewService>();
        services.AddScoped<ISalaryConfigService, SalaryConfigService>();
        services.AddScoped<SalarySettlementJob>();
        services.AddScoped<BScoreResetJob>();
        services.AddScoped<PromotionScanJob>();

        return services;
    }
}
