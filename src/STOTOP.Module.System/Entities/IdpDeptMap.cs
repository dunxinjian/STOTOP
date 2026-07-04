using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>
/// 部门映射（IDP部门映射，M8）。外部企业部门 ↔ SYS组织架构 节点，供部门同步/回调落地。
/// 实现 <see cref="ITenantScoped"/> 进租户硬墙（防伪造回调跨租户写组织，design §9）。
/// <para>F组织ID 是【映射目标】列（映射到哪个组织节点），非组织隔离键 → 【不】实现 IOrgScoped
/// （否则会被组织过滤器按当前组织收窄，而部门同步/回调在无特定组织上下文下按租户查全量映射）。</para>
/// </summary>
public class IdpDeptMap : BaseEntity, ITenantScoped
{
    /// <summary>租户ID（R9 隔离键）</summary>
    public long FTenantId { get; set; }

    /// <summary>外部企业 CorpId（→ IDP外部企业.FCorpId）</summary>
    public string FExternalCorpId { get; set; } = string.Empty;

    /// <summary>外部部门ID（corp 内的 deptId）</summary>
    public string FExternalDeptId { get; set; } = string.Empty;

    /// <summary>映射到的组织节点 FID（→ SYS组织架构.FID，映射目标列非隔离键）</summary>
    public long FOrgId { get; set; }

    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
