using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 附加费目的地
/// </summary>
public class ExpPriceSurchargeItemDest : BaseEntity
{
    /// <summary>配置项ID</summary>
    public long FSurchargeItemId { get; set; }
    /// <summary>目的地类型 1省 2市</summary>
    public int FDestType { get; set; } = 1;
    /// <summary>省份ID</summary>
    public int? FProvinceId { get; set; }
    /// <summary>城市名</summary>
    public string? FCityName { get; set; }
}
