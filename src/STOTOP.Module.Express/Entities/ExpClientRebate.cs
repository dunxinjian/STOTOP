using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 客户返利
/// </summary>
public class ExpClientRebate : BaseEntity, IOrgScoped
{
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>品牌ID</summary>
    public string? FBrandCode { get; set; }
    /// <summary>返利名称</summary>
    public string FRebateName { get; set; } = string.Empty;
    /// <summary>返利周期 1月 2季 3年</summary>
    public int FRebateCycle { get; set; } = 1;
    /// <summary>计算方式 1每票 2比例 3按重量 4阶梯</summary>
    public int FCalcMethod { get; set; }
    /// <summary>固定金额</summary>
    public decimal? FFixedAmount { get; set; }
    /// <summary>比例</summary>
    public decimal? FRatio { get; set; }
    /// <summary>重量单价</summary>
    public decimal? FWeightPrice { get; set; }
    /// <summary>最低票数</summary>
    public int? FMinTickets { get; set; }
    /// <summary>生效日期</summary>
    public DateTime? FEffectiveDate { get; set; }
    /// <summary>失效日期</summary>
    public DateTime? FExpiryDate { get; set; }
    /// <summary>是否启用</summary>
    public bool FIsActive { get; set; } = true;
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
