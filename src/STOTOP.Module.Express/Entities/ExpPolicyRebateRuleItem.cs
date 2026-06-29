using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 政策返利规则条件明细
/// </summary>
public class ExpPolicyRebateRuleItem : BaseEntity
{
    /// <summary>规则ID</summary>
    public long FRuleId { get; set; }
    /// <summary>阈值下限</summary>
    public decimal? FThresholdLower { get; set; }
    /// <summary>阈值上限</summary>
    public decimal? FThresholdUpper { get; set; }
    /// <summary>起始重量</summary>
    public decimal? FWeightFrom { get; set; }
    /// <summary>截止重量</summary>
    public decimal? FWeightTo { get; set; }
    /// <summary>省份ID</summary>
    public int? FProvinceId { get; set; }
    /// <summary>调整方向 1奖励 2处罚</summary>
    public int? FAdjustType { get; set; }
    /// <summary>调整计算方式 1固定金额 2按比例</summary>
    public int? FAdjustCalcMethod { get; set; }
    /// <summary>调整金额</summary>
    public decimal? FAdjustAmount { get; set; }
    /// <summary>调整比例</summary>
    public decimal? FAdjustRate { get; set; }
    /// <summary>排序</summary>
    public int FSortOrder { get; set; }
}
