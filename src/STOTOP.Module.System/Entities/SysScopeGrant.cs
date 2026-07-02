using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>R8 数据范围动作。</summary>
public enum ScopeAction
{
    Read = 1,
    Write = 2,
    All = 3,
}

/// <summary>R8 授权来源。</summary>
public enum ScopeGrantSource
{
    /// <summary>任职派生（RecomputeScopeGrants 自动维护，重算时先删本类）</summary>
    Derived = 1,
    /// <summary>手工授权（(Write,集团) 须二人复核 FApprovalId）</summary>
    Manual = 2,
}

/// <summary>
/// 数据范围授权（SYS数据范围授权，R8 多租户阶段2D）。用户可视/可改的组织范围物化(§7)。
/// 派生授权由 <see cref="Services.IScopeGrantService.RecomputeScopeGrantsAsync"/> 从任职(FScopeEligible)的物化范围根算出;
/// 查询期 <c>GetVisibleNodeIds</c> 经 SYS组织闭包 展开为可视节点集 + 租户二次夹逼,落 <c>ApplyVisibilityScope</c> 仓储扩展(不进全局过滤器)。
/// 实现 <see cref="ITenantScoped"/>(仅请求内被消费,挂墙安全);F范围节点ID 非 F组织ID,门禁不触发。
/// </summary>
public class SysScopeGrant : BaseEntity, ITenantScoped
{
    public long FUserId { get; set; }
    public long FTenantId { get; set; }

    /// <summary>范围类型（<see cref="OrgScopeType"/>：1集团/2区域公司/3中心/4网点公司）</summary>
    public int FScopeType { get; set; }

    /// <summary>范围节点ID（= 组织范围根节点；集团级时为租户根）</summary>
    public long FScopeNodeId { get; set; }

    /// <summary>范围动作（<see cref="ScopeAction"/>：1读/2写/3全部）</summary>
    public int FScopeAction { get; set; } = (int)ScopeAction.Read;

    /// <summary>授权来源（<see cref="ScopeGrantSource"/>：1派生/2手工）</summary>
    public int FGrantSource { get; set; } = (int)ScopeGrantSource.Derived;

    /// <summary>审批单ID：手工 (Write,集团) 授权必填（D6 二人复核）</summary>
    public long? FApprovalId { get; set; }

    /// <summary>到期时间：临时授权</summary>
    public DateTime? FExpireAt { get; set; }

    public int FStatus { get; set; } = 1;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
