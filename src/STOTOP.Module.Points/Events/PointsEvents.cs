using STOTOP.Infrastructure.Events;
using STOTOP.Module.Points.Constants;

namespace STOTOP.Module.Points.Events;

public class PointApplicationSubmittedEvent : BusinessEvent
{
    public long ApplicationId { get; set; }
    public long ApplicantId { get; set; }
    public int RequestedPoints { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>账户类型（1=A / 2=B）。默认 B 分。</summary>
    public int AccountType { get; set; } = PointAccountTypes.B;
}

public class PointApplicationApprovedEvent : BusinessEvent
{
    public long ApplicationId { get; set; }
    public long ApplicantId { get; set; }
    public int ApprovedPoints { get; set; }
    public long ApproverId { get; set; }
    /// <summary>账户类型（1=A / 2=B）。</summary>
    public int AccountType { get; set; } = PointAccountTypes.B;
}

public class PointApplicationRejectedEvent : BusinessEvent
{
    public long ApplicationId { get; set; }
    public long ApplicantId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long ApproverId { get; set; }
    /// <summary>账户类型（1=A / 2=B）。</summary>
    public int AccountType { get; set; } = PointAccountTypes.B;
}

/// <summary>
/// 积分获得事件 - PointService.AwardAsync 或事件触发奖分写入流水后发布。
/// 供 KSF/PPV/排行榜等下游模块联动消费。
/// </summary>
public class PointEarnedEvent : BusinessEvent
{
    /// <summary>积分所属用户</summary>
    public long UserId { get; set; }
    /// <summary>账户类型（1=A / 2=B）</summary>
    public int AccountType { get; set; } = PointAccountTypes.B;
    /// <summary>本次获得的积分值（正数）</summary>
    public int Amount { get; set; }
    /// <summary>来源事件类型（如 TASK_COMPLETE / QUALITY_EXCELLENT，手动奖分时为 null）</summary>
    public string? RelatedEventType { get; set; }
    /// <summary>来源事件ID（与 RelatedEventType 联合构成幂等键，手动奖分时为 null）</summary>
    public string? RelatedEventId { get; set; }
    /// <summary>命中的规则 ID（手动奖分时为 null）</summary>
    public long? RuleId { get; set; }
}

/// <summary>
/// 积分扣减事件 - PointService.DeductAsync 或事件触发扣分写入流水后发布。
/// 仅作用于 B 分账户（业务规则：A 分不可扣减）。
/// </summary>
public class PointDeductedEvent : BusinessEvent
{
    /// <summary>积分所属用户</summary>
    public long UserId { get; set; }
    /// <summary>账户类型（1=A / 2=B），扣分仅允许 B</summary>
    public int AccountType { get; set; } = PointAccountTypes.B;
    /// <summary>本次扣减的积分值（正数，绝对值）</summary>
    public int Amount { get; set; }
    /// <summary>来源事件类型（手动扣分时为 null）</summary>
    public string? RelatedEventType { get; set; }
    /// <summary>来源事件ID（手动扣分时为 null）</summary>
    public string? RelatedEventId { get; set; }
    /// <summary>命中的规则 ID（手动扣分时为 null）</summary>
    public long? RuleId { get; set; }
    /// <summary>扣减原因（备注/说明）</summary>
    public string? Reason { get; set; }
}


