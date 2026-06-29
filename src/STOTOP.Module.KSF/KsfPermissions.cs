namespace STOTOP.Module.KSF;

/// <summary>
/// KSF 模块权限编码常量（dot 分隔风格）
/// </summary>
public static class KsfPermissions
{
    // 指标管理
    public const string IndicatorView = "ksf.indicator.view";
    public const string IndicatorEdit = "ksf.indicator.edit";

    // 岗位方案
    public const string PlanView = "ksf.plan.view";
    public const string PlanEdit = "ksf.plan.edit";
    public const string PlanActivate = "ksf.plan.activate";

    // 核算结果
    public const string ResultView = "ksf.result.view";
    public const string ResultRecalc = "ksf.result.recalc";

    // 员工经营单元映射
    public const string MappingView = "ksf.mapping.view";
    public const string MappingEdit = "ksf.mapping.edit";
}
