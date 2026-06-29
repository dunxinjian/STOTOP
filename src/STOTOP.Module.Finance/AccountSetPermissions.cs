namespace STOTOP.Module.Finance;

/// <summary>
/// 账套级权限编码常量
/// </summary>
public static class AccountSetPermissions
{
    // 凭证权限
    public const string VoucherView = "accountset:voucher:view";
    public const string VoucherCreate = "accountset:voucher:create";
    public const string VoucherEdit = "accountset:voucher:edit";
    public const string VoucherDelete = "accountset:voucher:delete";
    public const string VoucherSubmit = "accountset:voucher:submit";
    public const string VoucherAudit = "accountset:voucher:audit";
    public const string VoucherUnaudit = "accountset:voucher:unaudit";
    public const string VoucherPrint = "accountset:voucher:print";

    // 期间权限
    public const string PeriodView = "accountset:period:view";
    public const string PeriodClose = "accountset:period:close";
    public const string PeriodReopen = "accountset:period:reopen";

    // 报表权限
    public const string ReportView = "accountset:report:view";
    public const string ReportExport = "accountset:report:export";

    // 科目权限
    public const string SubjectView = "accountset:subject:view";
    public const string SubjectEdit = "accountset:subject:edit";

    // 辅助核算权限
    public const string AuxiliaryView = "accountset:auxiliary:view";
    public const string AuxiliaryEdit = "accountset:auxiliary:edit";

    // 账套设置权限
    public const string SettingsView = "accountset:settings:view";
    public const string SettingsEdit = "accountset:settings:edit";

    // 出纳日记账权限
    public const string CashJournalView = "accountset:cashjournal:view";
    public const string CashJournalEdit = "accountset:cashjournal:edit";

    // 账套授权管理
    public const string AuthorizationManage = "accountset:authorization:manage";
}
