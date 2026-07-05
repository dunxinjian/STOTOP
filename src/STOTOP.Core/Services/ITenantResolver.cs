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

    /// <summary>
    /// 解析指定组织所属的租户 id（读 SYS组织架构.F租户ID）。
    /// 供批次 / 后台链路按批次组织确定租户，避免一律用 <see cref="GetRootTenantId"/> 导致多客户下串租户 / 漏处理。
    /// 组织无有效租户列（存量 0 / 未回填）、orgId 非法或查询失败 → 兜底 <see cref="GetRootTenantId"/>。结果按 org 缓存。
    /// </summary>
    long? ResolveTenantForOrg(long orgId);
}
