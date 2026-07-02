using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>
/// 租户成员（SYS租户成员，M3 多租户阶段2）。用户 ↔ 租户 归属 + R6 切换依据。
/// <para>
/// **跨租户实体，刻意不实现 ITenantScoped、不进 fail-closed 硬墙**——一个用户可属多个独立租户(客户)，
/// 切换列表须能看到用户在所有租户的成员行;进单租户硬墙会看不到其他租户成员。查询按 F用户ID 收窄,无 FOrgId 故漏标门禁不触发。
/// </para>
/// </summary>
public class SysTenantMember : BaseEntity
{
    /// <summary>用户ID（→ SYS用户.FID）</summary>
    public long FUserId { get; set; }

    /// <summary>租户ID（→ 租户根组织 FID；单客户 = MDSTO）</summary>
    public long FTenantId { get; set; }

    /// <summary>是否主租户（多租户默认进入哪个）</summary>
    public bool FIsPrimary { get; set; }

    /// <summary>邀请状态：1=待确认 / 2=已接受 / 3=已拒绝（存量回填 = 已接受）</summary>
    public int FInviteStatus { get; set; } = 2;

    /// <summary>邀请人（→ SYS用户.FID）</summary>
    public long? FInvitedBy { get; set; }

    /// <summary>加入时间</summary>
    public DateTime? FJoinedAt { get; set; }

    public int FStatus { get; set; } = 1;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
