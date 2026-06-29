using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 政策返利方案
/// </summary>
public class ExpPolicyRebate : BaseEntity, IOrgScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>品牌ID</summary>
    public string FBrandCode { get; set; } = string.Empty;
    /// <summary>方案名称</summary>
    public string FPolicyName { get; set; } = string.Empty;
    /// <summary>返利模式 1通票 2阶梯</summary>
    public int FRebateMode { get; set; }
    /// <summary>通票返利金额（仅模式1）</summary>
    public decimal? FFlatRebateAmount { get; set; }
    /// <summary>结算周期 1日 2周 3月</summary>
    public int FSettlementCycle { get; set; } = 3;
    /// <summary>生效日期</summary>
    public DateTime FEffectiveDate { get; set; }
    /// <summary>失效日期</summary>
    public DateTime? FExpiryDate { get; set; }
    /// <summary>状态 0草稿 1生效 2停用 3已归档</summary>
    public int FStatus { get; set; }
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
}
