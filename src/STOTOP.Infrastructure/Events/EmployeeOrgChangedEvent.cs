namespace STOTOP.Infrastructure.Events;

/// <summary>
/// 员工组织/岗位变更事件。
///
/// 设计说明：
/// - 本事件本应放在 STOTOP.Core.Contracts.Hr.Events，但 STOTOP.Core 不引用 STOTOP.Infrastructure，
///   而 BusinessEvent 基类位于 STOTOP.Infrastructure，故落地于本目录避免反向依赖。
/// - 业务模块（KSF / PPV / Points 等）通过 IEventHandler&lt;EmployeeOrgChangedEvent&gt; 订阅。
///
/// F变更类型 枚举：
///   1 = 入职
///   2 = 转岗
///   3 = 离职
///   4 = 主岗变更
///   5 = 主组织变更
///   6 = 直属上级变更
///   7 = 职级变更
/// </summary>
public class EmployeeOrgChangedEvent : BusinessEvent
{
    /// <summary>
    /// 变更类型。1=入职 2=转岗 3=离职 4=主岗变更 5=主组织变更 6=直属上级变更 7=职级变更
    /// </summary>
    public int F变更类型 { get; set; }

    /// <summary>
    /// 旧值快照 JSON（可空）。
    /// </summary>
    public string? F旧值Json { get; set; }

    /// <summary>
    /// 新值快照 JSON（可空）。
    /// </summary>
    public string? F新值Json { get; set; }

    /// <summary>
    /// 变更生效日期。
    /// </summary>
    public DateTime F生效日期 { get; set; }

    /// <summary>
    /// 被变更的员工 UserId（区别于基类 TriggeredByUserId — 后者表示操作者）。
    /// </summary>
    public long F关联用户ID { get; set; }
}
