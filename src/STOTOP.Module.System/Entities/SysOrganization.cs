using System.ComponentModel;
using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysOrganization : BaseEntity
{
    public string FUID { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string FCode { get; set; } = string.Empty;
    public long FParentId { get; set; } = 0;

    /// <summary>组织类型ID，外键 -> SYS组织类型.FID</summary>
    public long FTypeId { get; set; } = 5; // 默认为部门

    /// <summary>组织类型字符串（兴容过渡期，请改用 FTypeId）</summary>
    [Obsolete("Use FTypeId instead")]
    public string FType { get; set; } = "部门";

    public int FSort { get; set; } = 0;
    public int FStatus { get; set; } = 1;
    public string? FDingTalkDeptId { get; set; }
    public int FDingTalkBindStatus { get; set; }
    public string? FDingTalkDeptName { get; set; }
    public long? FManagerId { get; set; }
    public int? FHeadcount { get; set; }
    public bool FIsSwitchable { get; set; }
    public string? FDescription { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // ===== M4 多租户阶段2 组织模型（由 OrgTreeMaterializer 物化维护）=====

    /// <summary>租户ID（= 本节点所属租户根 FID；单客户下 = 组织树根 MDSTO）。
    /// 说明：组织树是租户结构骨架，携带 F租户ID 供多租户按租户裁剪，但**不**实现 ITenantScoped、不进 fail-closed 硬墙——
    /// 组织树在登录/切换等尚未确立租户上下文的引导路径被读取(中间件对这些路径 skip)，进硬墙会读空自锁。多租户组织可视性靠 R8 + 服务层租户过滤。</summary>
    public long FTenantId { get; set; } = 0;

    /// <summary>组织类别（<see cref="OrgKind"/>：0集团/1区域公司/2网点公司/3中心/4部门/5班组），单一真源，派生自 FTypeId。</summary>
    public int FKind { get; set; } = (int)OrgKind.Dept;

    /// <summary>物化：父节点 FKind（根为 null）。仅为让"合法父子"成为行内 CHECK 可判（跨行父子约束无法用普通 CHECK）。</summary>
    public int? FParentKind { get; set; }

    /// <summary>物化：最近网点公司(FKind=Company)祖先(含自身)的 FID；不在任何网点公司下为 null。</summary>
    public long? FCompanyId { get; set; }

    /// <summary>物化：R8 范围根节点 FID（ResolveScopeRoot）。</summary>
    public long FScopeRootId { get; set; } = 0;

    /// <summary>物化：R8 范围根类型（<see cref="OrgScopeType"/>：1集团/2区域公司/3中心/4网点公司）。</summary>
    public int FScopeRootType { get; set; } = (int)OrgScopeType.Group;

    /// <summary>物化：路径 /1/192/194/ 加速子树。</summary>
    public string? FPath { get; set; }

    /// <summary>物化：最近可切换祖先(含自身, FIsSwitchable=true)的 FID；无则 0。
    /// M3 用于 O(1) 切换列表(替代 FindSwitchableAncestor 的运行时上溯),语义与旧一致。</summary>
    public long FSwitchRootId { get; set; } = 0;

    /// <summary>并发令牌（供树变更同事务重算的乐观锁）。</summary>
    public byte[]? FRowVersion { get; set; }

    // 导航属性
    public virtual SysOrgType? OrgType { get; set; }
    public virtual SysUser? Manager { get; set; }
    public virtual ICollection<SysUserOrganization> UserOrganizations { get; set; } = new List<SysUserOrganization>();
    public virtual ICollection<SysPositionDepartment> PositionDepartments { get; set; } = new List<SysPositionDepartment>();
}
