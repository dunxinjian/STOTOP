using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

/// <summary>
/// 平台订阅（PLT订阅，多租户阶段4·平台层）。记录某租户对某套餐的一个计费周期。
/// <para>
/// 平台层·不实现 <see cref="ITenantScoped"/>：<see cref="FTenantId"/> 是指向 PLT租户 的普通外键列（非隔离键），
/// 本表由平台接口跨租户读写、不进租户硬墙。无 F组织ID → 漏标门禁不触发（门禁只针对 F组织ID）。
/// </para>
/// </summary>
public class PltSubscription : BaseEntity
{
    /// <summary>租户ID（→ PLT租户.FID，普通外键，非隔离键）</summary>
    public long FTenantId { get; set; }

    /// <summary>套餐ID（→ PLT套餐.FID）</summary>
    public long FPlanId { get; set; }

    /// <summary>周期起</summary>
    public DateTime FPeriodStart { get; set; }

    /// <summary>周期止</summary>
    public DateTime FPeriodEnd { get; set; }

    public int FStatus { get; set; } = 1;

    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
