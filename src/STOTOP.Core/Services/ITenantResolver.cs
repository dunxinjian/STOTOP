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
    /// 解析指定组织所属的租户 id（= 该组织 SYS组织架构.F租户ID，由 OrgTreeMaterializer 物化为其租户根组织 FID）。
    /// <para>兜底：orgId&lt;=0 或查不到该组织、或其 F租户ID&lt;=0（新建未物化/回填遗漏的瞬时值）时，回退到 <see cref="GetRootTenantId"/> 的根租户，避免租户上下文被解析成 0 而触发 fail-closed 读空/写抛。</para>
    /// <para>过渡期(单客户)：所有 org 的 F租户ID 都=组织树根，故任意 orgId 均返回根租户，与 <see cref="GetRootTenantId"/> 行为一致；多客户上线后各租户子树物化不同 F租户ID，本方法自动按批次/请求组织解析到正确租户。</para>
    /// </summary>
    long? ResolveTenantForOrg(long orgId);
}
