using Microsoft.Extensions.DependencyInjection;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.Finance.EventHandlers;
using STOTOP.Module.Finance.Events;
using STOTOP.Module.Finance.Services;
using STOTOP.Module.Finance.Services.FormulaEngine;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance;

public static class FinanceModuleExtensions
{
    /// <summary>
    /// 添加财务模块服务
    /// </summary>
    public static IServiceCollection AddFinanceModule(this IServiceCollection services)
    {
        // 注册服务
        services.AddScoped<IAccountPeriodService, AccountPeriodService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuxiliaryService, AuxiliaryService>();
        services.AddScoped<STOTOP.Module.Finance.Services.Interfaces.IVoucherService, VoucherService>();
        // CardFlow IVoucherService 桥接实现（供 CardFlow 模块跳过直接依赖调用凭证创建/红冲）
        services.AddScoped<STOTOP.Core.Interfaces.IVoucherService, CardFlowVoucherBridge>();
        services.AddScoped<VoucherRevokeHandler>();
        services.AddScoped<IDataScopeRevokeHandler>(sp => sp.GetRequiredService<VoucherRevokeHandler>());
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IAmoebaService, AmoebaService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<AmoebaPLService>();
        services.AddScoped<ICommonCostAllocationEngine, CommonCostAllocationEngine>();
        services.AddScoped<AccountSetService>();
        services.AddScoped<JournalService>();
        services.AddScoped<OperationLogService>();
        services.AddScoped<ChangeTrackingService>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<VoucherTemplateService>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddSingleton<IFormulaEngine, FormulaEngineImpl>();
        services.AddScoped<IFormulaService, FormulaService>();
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<ITrialBalanceService, TrialBalanceService>();
        services.AddScoped<IBankTransactionService, BankTransactionService>();
        services.AddScoped<IVoucherAutoService, VoucherAutoService>();
        services.AddScoped<IAccountTemplateService, AccountTemplateService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IBudgetExpenseMappingService, BudgetExpenseMappingService>();
        services.AddScoped<ITreasuryPlanService, TreasuryPlanService>();
        services.AddScoped<IBudgetOccupationService, BudgetOccupationService>();
        services.AddScoped<VoucherExcelService>();
        services.AddScoped<AuxiliaryAliasService>();
        services.AddScoped<IAccountSetAuthorizationService, AccountSetAuthorizationService>();
        services.AddScoped<IAccountSetRuleService, AccountSetRuleService>();
        services.AddScoped<MigrationMappingService>();

        // 事件处理器
        services.AddScoped<IEventHandler<VoucherPendingAuditEvent>, VoucherPendingAuditEventHandler>();
        services.AddScoped<IEventHandler<AccountPeriodClosedEvent>, AccountPeriodClosedEventHandler>();
        services.AddScoped<IEventHandler<AuxiliarySourceChangedEvent>, AuxiliarySourceChangedHandler>();

        return services;
    }
}
