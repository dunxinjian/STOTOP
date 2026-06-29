using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 成本方案_成本项_关联店铺（一口价专用）
/// </summary>
public class ExpCostPlanItemShop : BaseEntity
{
    /// <summary>成本项ID</summary>
    public long FItemId { get; set; }
    /// <summary>店铺名称</summary>
    public string FShopName { get; set; } = string.Empty;
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;

    // 导航属性
    public ExpCostPlanItem Item { get; set; } = null!;
}
