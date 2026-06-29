namespace STOTOP.Module.Points;

/// <summary>
/// 积分模块权限编码常量
/// </summary>
public static class PointsPermissions
{
    // 积分来源
    public const string SourceManage = "points:source:manage";

    // 积分规则
    public const string RuleView = "points:rule:view";
    public const string RuleManage = "points:rule:manage";

    // 积分流水
    public const string RecordView = "points:record:view";
    public const string Award = "points:award";
    public const string Deduct = "points:deduct";

    // 积分申请
    public const string ApplicationSubmit = "points:application:submit";
    public const string ApplicationApprove = "points:application:approve";

    // 兑换商品
    public const string RedeemView = "points:redeem:view";
    public const string RedeemManage = "points:redeem:manage";
    public const string RedeemExchange = "points:redeem:exchange";
    public const string RedeemDeliver = "points:redeem:deliver";

    // 管理层配额
    public const string QuotaView = "points:quota:view";
    public const string QuotaManage = "points:quota:manage";

    // 排行榜
    public const string RankingView = "points:ranking:view";

    // 积分看板
    public const string DashboardView = "points:dashboard:view";
}
