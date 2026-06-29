using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 成本方案_互斥配置（一口价与其他项的互斥关系，有时间链）
/// </summary>
public class ExpCostPlanExclusion : BaseEntity
{
    /// <summary>方案ID</summary>
    public long FPlanId { get; set; }
    /// <summary>生效日期</summary>
    public DateTime FEffectiveDate { get; set; }
    /// <summary>互斥规则JSON（被排除的成本项ID列表）</summary>
    public string? FExclusionRuleJson { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // 导航属性
    public ExpCostPlan Plan { get; set; } = null!;
}
