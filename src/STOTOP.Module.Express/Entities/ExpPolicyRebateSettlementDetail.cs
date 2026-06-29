using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 政策返利结算明细
/// </summary>
public class ExpPolicyRebateSettlementDetail : BaseEntity
{
    /// <summary>结算ID</summary>
    public long FSettlementId { get; set; }
    /// <summary>规则ID</summary>
    public long? FRuleId { get; set; }
    /// <summary>规则条件ID</summary>
    public long? FRuleItemId { get; set; }
    /// <summary>实际值</summary>
    public decimal? FActualValue { get; set; }
    /// <summary>阈值</summary>
    public decimal? FThresholdValue { get; set; }
    /// <summary>调整方向</summary>
    public int? FAdjustType { get; set; }
    /// <summary>调整金额</summary>
    public decimal? FAdjustAmount { get; set; }
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
}
