namespace STOTOP.Module.System.Entities;

/// <summary>
/// 组织闭包表（SYS组织闭包，M4 多租户阶段2）。存 (祖先, 后代, 层差) 全对，O(1) 取祖先链 / 后代子树，
/// 供 R8 数据范围(VisibleNodeIds) 与阿米巴上卷使用。含自反行(祖先=后代, 层差=0)。
/// 由 <see cref="Services.OrgTreeMaterializer"/> 从邻接树(SysOrganization.FParentId) 物化重建。
/// <para>
/// 注意：本表是租户结构骨架(非被隔离的业务行)，携带 F租户ID 供多租户按租户裁剪，但**不**实现 ITenantScoped、
/// 不进 fail-closed 硬墙——它在切换/引导等"尚未确立租户上下文"的路径被读取，进硬墙会导致读空(自锁)。
/// </para>
/// </summary>
public class SysOrgClosure
{
    /// <summary>祖先节点 FID</summary>
    public long FAncestorId { get; set; }

    /// <summary>后代节点 FID</summary>
    public long FDescendantId { get; set; }

    /// <summary>层差（祖先到后代的距离；自反=0）</summary>
    public int FDepth { get; set; }

    /// <summary>租户ID（= 后代节点所属租户根）</summary>
    public long FTenantId { get; set; }
}
