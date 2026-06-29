using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 政策返利结算
/// </summary>
public class ExpPolicyRebateSettlement : BaseEntity, IOrgScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>政策返利ID</summary>
    public long FPolicyRebateId { get; set; }
    /// <summary>品牌ID</summary>
    public string FBrandCode { get; set; } = string.Empty;
    /// <summary>品牌ID（数字类型）</summary>
    public long F品牌ID { get; set; }
    /// <summary>账期开始</summary>
    public DateTime FPeriodStart { get; set; }
    /// <summary>账期结束</summary>
    public DateTime FPeriodEnd { get; set; }
    /// <summary>总单量</summary>
    public int? FTotalWaybills { get; set; }
    /// <summary>总重量</summary>
    public decimal? FTotalWeight { get; set; }
    /// <summary>平均重量</summary>
    public decimal? FAvgWeight { get; set; }
    /// <summary>基础返利</summary>
    public decimal? FBaseRebateAmount { get; set; }
    /// <summary>总奖励</summary>
    public decimal? FTotalReward { get; set; }
    /// <summary>总处罚</summary>
    public decimal? FTotalPenalty { get; set; }
    /// <summary>最终返利</summary>
    public decimal? FFinalRebateAmount { get; set; }
    /// <summary>状态 0待确认 1已确认 2已核销</summary>
    public int FStatus { get; set; }
    /// <summary>确认人</summary>
    public string? FConfirmedBy { get; set; }
    /// <summary>确认时间</summary>
    public DateTime? FConfirmedTime { get; set; }
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
