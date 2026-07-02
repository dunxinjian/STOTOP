using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>
/// 任职（SYS任职，M3 多租户阶段2）。成员在组织节点的任职，喂 R8 数据范围(FScopeEligible → RecomputeScopeGrants)。
/// 实现 <see cref="ITenantScoped"/>（进 fail-closed 租户硬墙）；F组织ID 是任职节点的普通列(非隔离键，故不实现 IOrgScoped——
/// R8 重算须跨用户全部任职读取，若按当前组织单节点过滤会漏)。只在请求内(租户已置)被 R8 消费,不在登录/切换引导路径读,故挂硬墙安全。
/// </summary>
public class SysAppointment : BaseEntity, ITenantScoped
{
    /// <summary>租户ID（R9 隔离键）</summary>
    public long FTenantId { get; set; }

    /// <summary>成员ID（→ SYS租户成员.FID）</summary>
    public long FMemberId { get; set; }

    /// <summary>任职组织节点（→ SYS组织架构.FID）</summary>
    public long FOrgId { get; set; }

    /// <summary>直属上级（→ SYS用户.FID）</summary>
    public long? FDirectSuperiorId { get; set; }

    /// <summary>是否主任职（喂 R8 的主任职）</summary>
    public bool FIsPrimary { get; set; }

    /// <summary>是否可参与范围放大（主任职默认 true / 非主默认 false；挂名/借调不放大范围）</summary>
    public bool FScopeEligible { get; set; }

    public string? FPosition { get; set; }
    public string? FJobNumber { get; set; }
    public DateTime? FEntryDate { get; set; }

    /// <summary>是否在职（历史任职 = false）</summary>
    public bool FIsCurrent { get; set; } = true;

    public int FStatus { get; set; } = 1;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
