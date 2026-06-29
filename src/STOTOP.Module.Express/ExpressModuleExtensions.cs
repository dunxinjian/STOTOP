using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.CardFlow.AutoPlugin;
using STOTOP.Module.Express.EventHandlers;
using STOTOP.Module.Express.Services;
using STOTOP.Module.Express.Services.Agents;
using STOTOP.Module.Express.Services.Billing;

namespace STOTOP.Module.Express;

public static class ExpressModuleExtensions
{
    /// <summary>
    /// 添加快递模块服务
    /// </summary>
    public static IServiceCollection AddExpressModule(this IServiceCollection services)
    {
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IShopService, ShopService>();
        services.AddScoped<IProvinceService, ProvinceService>();
        services.AddScoped<INetworkPointService, NetworkPointService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IFranchiseAreaService, FranchiseAreaService>();
        services.AddScoped<ILastMileStationService, LastMileStationService>();
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<IPricePlanImportService, PricePlanImportService>();
        services.AddScoped<IPriceSurchargeService, PriceSurchargeService>();
        services.AddScoped<ICostItemService, CostItemService>();
        services.AddScoped<ICostPlanService, CostPlanService>();
        services.AddScoped<IWaybillService, WaybillService>();
        services.AddScoped<IWaybillImportService, WaybillImportService>();
        services.AddScoped<ShopAutoDiscoveryJob>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<PricingEngine>();
        services.AddScoped<CostEngine>();
        services.AddScoped<BillingBulkWriter>();
        // 导入计算验证工作台的价格解释（接口定义在 CardFlow，避免 CardFlow→Express 反向依赖）
        services.AddScoped<STOTOP.Module.CardFlow.Services.Validation.IPricingExplainProvider, PricingExplainProvider>();
        services.AddSingleton<ProvinceCache>();
        services.AddScoped<PricingPlugin>();
        services.AddScoped<IQualityIssueTypeProvider>(sp => sp.GetRequiredService<PricingPlugin>());
        services.AddScoped<CostPlugin>();
        services.AddScoped<IQualityIssueTypeProvider>(sp => sp.GetRequiredService<CostPlugin>());

        // 账单管理
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IInvoiceReviewService, InvoiceReviewService>();
        services.AddScoped<InvoiceGeneratorJob>();
        // 预付款管理
        services.AddScoped<IPrepaymentService, PrepaymentService>();
        // 运单号管理
        services.AddScoped<IWaybillNumberService, WaybillNumberService>();
        // 政策返利
        services.AddScoped<IPolicyRebateService, PolicyRebateService>();
        services.AddScoped<PolicyRebateCalcEngine>();
        services.AddScoped<IPolicyRebateSettlementService, PolicyRebateSettlementService>();
        services.AddScoped<PolicyRebateSimulator>();

        // 归档
        services.AddScoped<IWaybillArchiveService, WaybillArchiveService>();
        // 统计报表
        services.AddScoped<IFlowAnalysisService, FlowAnalysisService>();
        services.AddScoped<IWeightSegmentReportService, WeightSegmentReportService>();
        services.AddScoped<IProfitAnalysisService, ProfitAnalysisService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // 数据质量中心
        services.AddScoped<IQualityCenterService, QualityCenterService>();

        // 事件处理器
        services.AddScoped<IEventHandler<WorkItemStatusChangedEvent>, WorkItemStatusChangedHandler>();

        // 业务员管理
        services.AddScoped<ISalesmanService, SalesmanService>();
        // 用户网点权限
        services.AddScoped<IUserNetworkPermissionService, UserNetworkPermissionService>();

        return services;
    }
}
