using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 客户省份配额（目的地占比）
/// </summary>
public class ExpClientProvinceQuota : BaseEntity, IOrgScoped
{
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>品牌ID</summary>
    public string? FBrandCode { get; set; }
    /// <summary>省份ID</summary>
    public int FProvinceId { get; set; }
    /// <summary>最大占比</summary>
    public decimal FMaxRatio { get; set; }
    /// <summary>超出计费方式 1每票 2每公斤</summary>
    public int FExcessCalcMethod { get; set; } = 1;
    /// <summary>超出金额</summary>
    public decimal FExcessAmount { get; set; }
    /// <summary>生效日期</summary>
    public DateTime? FEffectiveDate { get; set; }
    /// <summary>失效日期</summary>
    public DateTime? FExpiryDate { get; set; }
    /// <summary>启用</summary>
    public bool FEnabled { get; set; } = true;
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
