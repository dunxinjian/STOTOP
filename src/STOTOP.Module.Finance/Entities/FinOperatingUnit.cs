using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

/// <summary>
/// 经营单元（FIN经营单元，M6/R2 多租户阶段3）。从 SYS网点公司 **1:1 物化派生**(禁手工维护),报表按 F网点公司ID 分组即得维度。
/// 修正现状 business_unit 无独立实体、无区域上卷的缺口(阶段3C 把阿米巴 business_unit 报表迁到本表 + 闭包上卷)。
/// 实现 <see cref="ITenantScoped"/>(仅请求内被报表/KSF 消费,挂墙安全);无 FOrgId(公司级主数据,非组织级业务行)。
/// </summary>
public class FinOperatingUnit : BaseEntity, ITenantScoped
{
    public long FTenantId { get; set; }

    /// <summary>网点公司ID（→ SYS网点公司.FID，唯一 1:1 派生源）</summary>
    public long FCompanyId { get; set; }

    public string FCode { get; set; } = string.Empty;

    /// <summary>名称（派生自网点公司）</summary>
    public string FName { get; set; } = string.Empty;

    /// <summary>状态（公司停用联动停用）</summary>
    public int FStatus { get; set; } = 1;

    /// <summary>并发令牌</summary>
    public byte[]? FRowVersion { get; set; }

    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
