using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 客户均重上限
/// </summary>
public class ExpClientWeightCap : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>品牌ID</summary>
    public string? FBrandCode { get; set; }
    /// <summary>最大均重</summary>
    public decimal FMaxAvgWeight { get; set; }
    /// <summary>超出单位</summary>
    public decimal? FExcessUnit { get; set; }
    /// <summary>超出单价</summary>
    public decimal? FExcessUnitPrice { get; set; }
    /// <summary>生效日期</summary>
    public DateTime? FEffectiveDate { get; set; }
    /// <summary>失效日期</summary>
    public DateTime? FExpiryDate { get; set; }
    /// <summary>启用</summary>
    public bool FEnabled { get; set; } = true;
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
