using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// 待办分发日志（CF待办分发日志，多租户阶段4E·R4）。每个 (待办, 渠道) 分发时记一条，作两用：
/// <list type="number">
/// <item>【权威绑定】外部 taskId → (租户, 待办)——回调据 taskId 从本表精确定位待办与租户，替代 FExternalTodoId 尾缀模糊匹配，
///       杜绝伪造回调命中无关/跨租户待办（回调只能作用于有真实分发记录的 taskId）。</item>
/// <item>【幂等台账】记录已处理的回调事件（FLastCallbackEvent），同事件重放为 no-op（design R4 幂等键含租户）。</item>
/// </list>
/// 实现 <see cref="ITenantScoped"/>（租户级台账；回调匿名经 IgnoreQueryFilters 读）。不实现 IOrgScoped（无 FOrgId，回调无组织上下文）。
/// </summary>
public class CfTodoDispatchLog : BaseEntity, ITenantScoped
{
    /// <summary>租户ID（R9 隔离键）</summary>
    public long FTenantId { get; set; }

    /// <summary>待办项ID（→ CfTodoItem.FID）</summary>
    public long FTodoItemId { get; set; }

    /// <summary>渠道（dingtalk/wecom）</summary>
    public string FChannel { get; set; } = string.Empty;

    /// <summary>外部任务ID（钉钉/企微 taskId；回调按此精确匹配）</summary>
    public string? FExternalTaskId { get; set; }

    /// <summary>外部企业 CorpId（回调 corp→租户 校验用，当前可空，留 IDP 接线后填）</summary>
    public string? FCorpId { get; set; }

    /// <summary>分发状态（dispatched/failed）</summary>
    public string FDispatchStatus { get; set; } = "dispatched";

    /// <summary>已处理的最近一次回调事件（幂等标记：同事件重放跳过）</summary>
    public string? FLastCallbackEvent { get; set; }

    /// <summary>最近回调处理时间</summary>
    public DateTime? FLastCallbackAt { get; set; }

    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
