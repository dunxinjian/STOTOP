namespace STOTOP.Infrastructure.Events;

/// <summary>
/// 积分清算事件 - 由清算 Job 在月清/年清完成后发布。
/// 供 KSF/PPV/排行榜/工资等下游模块联动消费。
/// </summary>
public class PointResetEvent : BusinessEvent
{
    /// <summary>积分所属用户</summary>
    public long UserId { get; set; }
    /// <summary>账户类型（1=A / 2=B），清算通常仅作用于 B 分账户</summary>
    public int AccountType { get; set; } = 2;
    /// <summary>清算类型：1=月清 / 2=年清</summary>
    public int ResetType { get; set; }
    /// <summary>清算前的可用余额（结算基数）</summary>
    public int BalanceBeforeReset { get; set; }
    /// <summary>清算后的可用余额（通常为 0 或保留 A 分）</summary>
    public int BalanceAfterReset { get; set; }
    /// <summary>本次清算转换出的福利券面值（按规则配置的转换比例计算）</summary>
    public decimal ConvertedToVoucherValue { get; set; }
    /// <summary>清算周期键（月清=yyyy-MM / 年清=yyyy）</summary>
    public string PeriodKey { get; set; } = string.Empty;
}
