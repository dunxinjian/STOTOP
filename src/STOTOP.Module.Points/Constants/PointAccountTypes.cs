namespace STOTOP.Module.Points.Constants;

/// <summary>
/// 积分账户类型常量
/// A 分（终身资本化）/ B 分（周期清算）
/// 与 PmPointAccount.F账户类型 / PmPointRule.F账户类型 / PmPointRecord.F账户类型 / PmPointApplication.F账户类型 一一对应
/// </summary>
public static class PointAccountTypes
{
    /// <summary>A 分：终身资本化（不清算，不可消费）</summary>
    public const int A = 1;

    /// <summary>B 分：周期清算（可兑换/消费的可流通积分）</summary>
    public const int B = 2;
}
