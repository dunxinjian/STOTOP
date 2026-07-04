using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>
/// 平台租户（PLT租户，多租户阶段4·平台层）。租户 = 客户/订阅实体（design/23 v2：客户形态可变，集团/区域公司/网点公司均可作租户根）。
/// <para>
/// 【平台层·不实现 <see cref="ITenantScoped"/>】：本表在租户硬墙之上、定义"租户"本身，若挂租户过滤器会陷入
/// "查租户列表须先在某租户内"的鸡生蛋死锁。平台接口(/api/platform/*)物理脱离过滤器读写本表。无 F组织ID → 漏标门禁不触发。
/// </para>
/// <para>
/// 过渡期(单客户 MDSTO)：由 SystemSeeder V13 回填单行，<c>FID = 组织树根节点 FID</c>（IDENTITY_INSERT），
/// 使全表存量 <c>F租户ID=根组织id</c> 与本表主键一致——<see cref="Services.TenantResolver"/> 改读本表后返回值不变。
/// </para>
/// </summary>
public class PltTenant : BaseEntity
{
    /// <summary>租户名称（如"MDSTO"/"太仓美申"）</summary>
    public string FName { get; set; } = string.Empty;

    /// <summary>租户编号（唯一）</summary>
    public string FCode { get; set; } = string.Empty;

    /// <summary>对应组织树根节点 FID（SYS组织架构 F父ID=0 的区域公司/集团根）</summary>
    public long FRootOrgId { get; set; }

    /// <summary>账套绑定模式（D2）：1=按区域公司 / 2=按网点公司。租户级默认策略（各账套的实际绑定记于 FIN账套.FAccountSetBindMode）。</summary>
    public int FAccountSetBindMode { get; set; } = 1;

    /// <summary>默认待办渠道（D3）：1=钉钉 / 2=企微 / 3=双推。供 R4 待办分发选渠道（阶段4E 消费）。</summary>
    public int FDefaultTodoChannel { get; set; } = 1;

    /// <summary>套餐ID（→ PLT套餐，可空=未订阅具体套餐）</summary>
    public long? FPlanId { get; set; }

    /// <summary>开通时间</summary>
    public DateTime? FActivatedAt { get; set; }

    /// <summary>到期时间</summary>
    public DateTime? FExpireAt { get; set; }

    /// <summary>状态，见 <see cref="PltTenantStatus"/>：1=试用/2=正式/3=停用/4=欠费冻结</summary>
    public int FStatus { get; set; } = (int)PltTenantStatus.Active;

    /// <summary>并发令牌</summary>
    public byte[]? FRowVersion { get; set; }

    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}

/// <summary>平台租户状态（PLT租户.FStatus）。</summary>
public enum PltTenantStatus
{
    /// <summary>试用</summary>
    Trial = 1,
    /// <summary>正式</summary>
    Active = 2,
    /// <summary>停用（不可解析、拒登录）</summary>
    Disabled = 3,
    /// <summary>欠费冻结（可登录/续费/放行结账类只读，禁业务写与批量导出，D7）</summary>
    Frozen = 4,
}
