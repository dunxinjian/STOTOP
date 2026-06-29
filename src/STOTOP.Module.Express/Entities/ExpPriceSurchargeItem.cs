using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 附加费配置项
/// </summary>
public class ExpPriceSurchargeItem : BaseEntity
{
    /// <summary>附加费ID</summary>
    public long FSurchargeId { get; set; }
    /// <summary>计费方式</summary>
    public int FCalcMethod { get; set; }
    /// <summary>重量进位方式</summary>
    public int? FWeightRoundingMethod { get; set; }
    /// <summary>起始重量</summary>
    public decimal? FWeightFrom { get; set; }
    /// <summary>截止重量</summary>
    public decimal? FWeightTo { get; set; }
    /// <summary>重量类型 1实重 2计费重</summary>
    public int FWeightType { get; set; } = 1;
    /// <summary>日单量起</summary>
    public int? FDailyVolumeFrom { get; set; }
    /// <summary>日单量止</summary>
    public int? FDailyVolumeTo { get; set; }
    /// <summary>金额</summary>
    public decimal FAmount { get; set; }
    /// <summary>排序</summary>
    public int FSortOrder { get; set; } = 0;

    /// <summary>附加费</summary>
    public ExpPriceSurcharge Surcharge { get; set; } = null!;
    /// <summary>目的地</summary>
    public List<ExpPriceSurchargeItemDest> Destinations { get; set; } = new();
}
