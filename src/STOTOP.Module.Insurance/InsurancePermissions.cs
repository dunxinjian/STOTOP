namespace STOTOP.Module.Insurance;

public static class InsurancePermissions
{
    // 保险公司
    public const string CompanyView = "insurance:company:view";
    public const string CompanyEdit = "insurance:company:edit";
    // 保单
    public const string PolicyView = "insurance:policy:view";
    public const string PolicyCreate = "insurance:policy:create";
    public const string PolicyEdit = "insurance:policy:edit";
    // 出险
    public const string ClaimView = "insurance:claim:view";
    public const string ClaimCreate = "insurance:claim:create";
    public const string ClaimEdit = "insurance:claim:edit";
    public const string ClaimClose = "insurance:claim:close";
    // 理赔
    public const string SettlementView = "insurance:settlement:view";
    public const string SettlementCreate = "insurance:settlement:create";
    public const string SettlementSubmit = "insurance:settlement:submit";
    public const string SettlementReview = "insurance:settlement:review";
    public const string SettlementApprove = "insurance:settlement:approve";
    public const string SettlementPay = "insurance:settlement:pay";
    // 共保基金
    public const string FundView = "insurance:fund:view";
    public const string FundCreate = "insurance:fund:create";
    public const string FundEdit = "insurance:fund:edit";
    // 审批配置
    public const string ApprovalConfigView = "insurance:approval-config:view";
    public const string ApprovalConfigEdit = "insurance:approval-config:edit";
    // 报表
    public const string ReportView = "insurance:report:view";
}
