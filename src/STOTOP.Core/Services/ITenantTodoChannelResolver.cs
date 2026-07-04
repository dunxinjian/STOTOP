namespace STOTOP.Core.Services;

/// <summary>
/// 解析某租户的默认待办分发渠道（多租户阶段4·D3；闭合 4A 加的 PLT租户.FDefaultTodoChannel 字段）。
/// 跨模块契约：实现在 System(读 PLT租户)，供 CardFlow 待办派发消费——避免 CardFlow 直依赖 System 实体。
/// 返回渠道名列表（如 ["dingtalk"] / ["wecom"] / ["dingtalk","wecom"]）；解析不到返回空（调用方回退按待办自带渠道）。
/// </summary>
public interface ITenantTodoChannelResolver
{
    /// <summary>按租户返回其默认待办渠道名（1=钉钉→["dingtalk"]/2=企微→["wecom"]/3=双推→两者）。</summary>
    Task<IReadOnlyList<string>> ResolveChannelNamesAsync(long tenantId);
}
