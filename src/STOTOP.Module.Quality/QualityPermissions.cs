namespace STOTOP.Module.Quality;

/// <summary>
/// 质量中心模块权限编码常量
/// </summary>
public static class QualityPermissions
{
    // 质量看板
    public const string DashboardView = "quality:dashboard:view";

    // 异常管理
    public const string ExceptionView = "quality:exception:view";
    public const string ExceptionManage = "quality:exception:manage";

    // 规则引擎
    public const string RuleView = "quality:rule:view";
    public const string RuleManage = "quality:rule:manage";

    // 复盘管理
    public const string ReviewView = "quality:review:view";
    public const string ReviewManage = "quality:review:manage";

    // 知识库
    public const string KnowledgeView = "quality:knowledge:view";
    public const string KnowledgeManage = "quality:knowledge:manage";

    // 绩效查询
    public const string PerformanceView = "quality:performance:view";

    // 预警配置
    public const string AlertView = "quality:alert:view";
    public const string AlertManage = "quality:alert:manage";

    // 承运商质控（Plan 3 看板 + 主数据认领）
    public const string CarrierQualityView = "quality:carrier:view";
    public const string CarrierMasterDataClaim = "quality:carrier:claim";
}
