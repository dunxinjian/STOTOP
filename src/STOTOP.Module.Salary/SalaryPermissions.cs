namespace STOTOP.Module.Salary;

/// <summary>
/// Salary 模块权限编码常量（dot 分隔风格）
/// </summary>
public static class SalaryPermissions
{
    // 薪酬档位
    public const string GradeView = "sal.grade.view";
    public const string GradeEdit = "sal.grade.edit";

    // 员工薪酬档案
    public const string ArchiveView = "sal.archive.view";
    public const string ArchiveEdit = "sal.archive.edit";

    // 工资单
    public const string PayrollView = "sal.payroll.view";
    public const string PayrollAudit = "sal.payroll.audit";
    public const string PayrollRelease = "sal.payroll.release";

    // 晋升
    public const string PromotionView = "sal.promotion.view";
    public const string PromotionRuleEdit = "sal.promotion.rule.edit";
    public const string PromotionReview = "sal.promotion.review";
}
