namespace STOTOP.Core.Services;

/// <summary>
/// 解析当前上下文的【租户=客户】id（v2 多租户）。
/// 过渡期(单客户)：租户 = 组织树根(F父ID=0)节点 id，首次解析后缓存。
/// 多客户经 SaaS 上线后改为按用户成员关系(SYS租户成员)解析。
/// </summary>
public interface ITenantResolver
{
    /// <summary>当前库根客户(单客户过渡期租户)的 id；解析不到返回 null。</summary>
    long? GetRootTenantId();
}
