using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 账单审核规则
/// </summary>
public class ExpInvoiceReviewRule : BaseEntity, IOrgScoped
{
    /// <summary>规则名称</summary>
    public string FRuleName { get; set; } = string.Empty;
    /// <summary>规则类型 1单票均价范围 2总额偏差比 3单量偏差比 4异常运单比例 5均重范围 6其他</summary>
    public int FRuleType { get; set; }
    /// <summary>最小值</summary>
    public decimal? FMinValue { get; set; }
    /// <summary>最大值</summary>
    public decimal? FMaxValue { get; set; }
    /// <summary>阈值</summary>
    public decimal? FThreshold { get; set; }
    /// <summary>业务对象ID（F编号）</summary>
    public string? FBusinessObjectId { get; set; }
    /// <summary>品牌ID</summary>
    public string? FBrandCode { get; set; }
    /// <summary>优先级</summary>
    public int FPriority { get; set; }
    /// <summary>启用</summary>
    public bool FEnabled { get; set; } = true;
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
