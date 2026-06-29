namespace STOTOP.Module.Express;

/// <summary>
/// 快递模块权限编码常量
/// </summary>
public static class ExpressPermissions
{
    // 品牌
    public const string BrandView = "express:brand:view";
    public const string BrandCreate = "express:brand:create";
    public const string BrandEdit = "express:brand:edit";
    public const string BrandDelete = "express:brand:delete";
    // 店铺
    public const string ShopView = "express:shop:view";
    public const string ShopCreate = "express:shop:create";
    public const string ShopEdit = "express:shop:edit";
    public const string ShopDelete = "express:shop:delete";
    // 业务对象
    public const string ClientView = "express:client:view";
    public const string ClientCreate = "express:client:create";
    public const string ClientEdit = "express:client:edit";
    public const string ClientDelete = "express:client:delete";
    // 省份
    public const string ProvinceView = "express:province:view";
    public const string ProvinceManage = "express:province:manage";
    // 网点
    public const string NetworkPointView = "express:networkpoint:view";
    public const string NetworkPointCreate = "express:networkpoint:create";
    public const string NetworkPointEdit = "express:networkpoint:edit";
    public const string NetworkPointDelete = "express:networkpoint:delete";
    // 承包区
    public const string ContractAreaView = "express:contractarea:view";
    public const string ContractAreaCreate = "express:contractarea:create";
    public const string ContractAreaEdit = "express:contractarea:edit";
    public const string ContractAreaDelete = "express:contractarea:delete";
    // 店铺归属
    public const string ShopAssignmentView = "express:shopassignment:view";
    public const string ShopAssignmentManage = "express:shopassignment:manage";
    // 客户别名
    public const string ClientAliasView = "express:clientalias:view";
    public const string ClientAliasManage = "express:clientalias:manage";
    // 层级关系
    public const string HierarchyView = "express:hierarchy:view";
    public const string HierarchyManage = "express:hierarchy:manage";
    // 报价方案
    public const string PricePlanView = "express:priceplan:view";
    public const string PricePlanCreate = "express:priceplan:create";
    public const string PricePlanEdit = "express:priceplan:edit";
    public const string PricePlanDelete = "express:priceplan:delete";
    // 附加费
    public const string SurchargeView = "express:surcharge:view";
    public const string SurchargeCreate = "express:surcharge:create";
    public const string SurchargeEdit = "express:surcharge:edit";
    public const string SurchargeDelete = "express:surcharge:delete";
    // 成本项目
    public const string CostItemView = "express:costitem:view";
    public const string CostItemCreate = "express:costitem:create";
    public const string CostItemEdit = "express:costitem:edit";
    // 成本方案
    public const string CostPlanView = "express:costplan:view";
    public const string CostPlanCreate = "express:costplan:create";
    public const string CostPlanEdit = "express:costplan:edit";
    public const string CostPlanDelete = "express:costplan:delete";
    // 运单
    public const string WaybillView = "express:waybill:view";
    public const string WaybillImport = "express:waybill:import";
    // 店铺发现
    public const string ShopDiscovery = "express:shop:discovery";
    // 计费
    public const string BillingExecute = "express:billing:execute";
    public const string BillingView = "express:billing:view";
    // 账单
    public const string InvoiceView = "express:invoice:view";
    public const string InvoiceCreate = "express:invoice:create";
    public const string InvoiceEdit = "express:invoice:edit";
    public const string InvoiceDelete = "express:invoice:delete";
    public const string InvoiceReview = "express:invoice:review";
    public const string InvoiceGenerate = "express:invoice:generate";
    // 预付款
    public const string PrepaymentView = "express:prepayment:view";
    public const string PrepaymentCreate = "express:prepayment:create";
    // 运单号
    public const string WaybillNumberView = "express:waybill-number:view";
    public const string WaybillNumberCreate = "express:waybill-number:create";
    public const string WaybillNumberEdit = "express:waybill-number:edit";
    // 政策返利
    public const string PolicyRebateView = "express:policy-rebate:view";
    public const string PolicyRebateCreate = "express:policy-rebate:create";
    public const string PolicyRebateEdit = "express:policy-rebate:edit";
    public const string PolicyRebateDelete = "express:policy-rebate:delete";
    public const string PolicyRebateSimulate = "express:policy-rebate:simulate";
    // 政策返利结算
    public const string PolicyRebateSettlementView = "express:policy-rebate-settlement:view";
    public const string PolicyRebateSettlementExecute = "express:policy-rebate-settlement:execute";
    public const string PolicyRebateSettlementConfirm = "express:policy-rebate-settlement:confirm";
    // 归档
    public const string ArchiveView = "express:archive:view";
    public const string ArchiveExecute = "express:archive:execute";
    // 报表
    public const string ReportView = "express:report:view";
    // 看板
    public const string DashboardView = "express:dashboard:view";
    // 对账
    public const string ReconciliationView    = "express:reconciliation:view";
    public const string ReconciliationConfirm = "express:reconciliation:confirm";
    public const string ReconciliationDispute = "express:reconciliation:dispute";
    public const string ReconciliationResolve = "express:reconciliation:resolve";
    public const string ReconciliationExport  = "express:reconciliation:export";
    // 数据质量中心
    public const string QualityCenterView    = "express:quality-center:view";
    public const string QualityCenterManage  = "express:quality-center:manage";
    public const string QualityCenterRerun   = "express:quality-center:rerun";
}
