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

    /// <summary>来源类型（'SYS网点公司'：从网点公司派生；预留他源）</summary>
    public string? FSourceType { get; set; }

    /// <summary>
    /// 遗留业务单元 aux 交叉引用（→ FIN辅助核算项目.FID，FAuxType='business_unit'）。阶段3C 建桥：
    /// 报表/映射规则里的 business_unit aux id 经此归到经营单元(名/区域上卷取此)、且存量凭证/映射对 aux id 的引用不断链。
    /// 只覆盖**网点公司级** aux(城区/南郊/浏河/沙溪)；出港业务(方向)、太仓美申(区域) 无对应经营单元故为 null。
    /// </summary>
    public long? FSourceLegacyAuxId { get; set; }

    /// <summary>并发令牌</summary>
    public byte[]? FRowVersion { get; set; }

    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
