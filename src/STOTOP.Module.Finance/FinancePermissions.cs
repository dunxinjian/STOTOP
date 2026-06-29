namespace STOTOP.Module.Finance;

/// <summary>
/// 财务模块权限编码常量
/// </summary>
public static class FinancePermissions
{
    // 凭证权限
    public const string VoucherView   = "finance:voucher:view";
    public const string VoucherCreate = "finance:voucher:create";
    public const string VoucherEdit   = "finance:voucher:edit";
    public const string VoucherDelete = "finance:voucher:delete";
    public const string VoucherAudit  = "finance:voucher:audit";
    public const string VoucherPrint  = "finance:voucher:print";

    // 结账权限
    public const string PeriodClose  = "finance:period:close";
    public const string PeriodReopen = "finance:period:reopen";  // 反结账，仅主管

    // 日记账权限
    public const string JournalView   = "finance:journal:view";
    public const string JournalCreate = "finance:journal:create";
    public const string JournalAdjust = "finance:journal:adjust";

    // 报表权限
    public const string ReportView   = "finance:report:view";
    public const string ReportExport = "finance:report:export";

    // 科目管理
    public const string AccountManage    = "finance:account:manage";
    public const string AccountSetManage = "finance:accountset:manage";

    // 模板管理
    public const string TemplateManage = "finance:template:manage";

    // 银行对账
    public const string BankReconciliationView   = "finance:bank:view";
    public const string BankReconciliationImport = "finance:bank:import";
    public const string BankReconciliationMatch  = "finance:bank:match";

    // 发票管理
    public const string InvoiceManage = "finance:invoice:manage";

    // 公式配置管理
    public const string FormulaManage = "finance:formula:manage";

    // 资产折旧操作
    public const string AssetDepreciate = "finance:asset:depreciate";

    // 资金管理
    public const string BankChannelView = "finance:bankchannel:view";
    public const string BankChannelManage = "finance:bankchannel:manage";
    public const string BankTransactionView = "finance:banktransaction:view";
    public const string BankTransactionImport = "finance:banktransaction:import";
    public const string BankTransactionMatch = "finance:banktransaction:match";
    public const string VoucherRuleView = "finance:voucherrule:view";
    public const string VoucherRuleManage = "finance:voucherrule:manage";

    // 预算与资金计划
    public const string BudgetView = "finance:budget:view";
    public const string BudgetEdit = "finance:budget:edit";
    public const string BudgetApprove = "finance:budget:approve";
    public const string BudgetMapping = "finance:budget:mapping";
    public const string TreasuryView = "finance:treasury:view";
    public const string TreasuryEdit = "finance:treasury:edit";
    public const string BudgetControlView = "finance:budget-control:view";

}
