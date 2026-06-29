using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 成本方案_成本项_应用网点
/// </summary>
public class ExpCostPlanItemOutlet : BaseEntity
{
    /// <summary>成本项ID</summary>
    public long FItemId { get; set; }
    /// <summary>网点ID</summary>
    public long FOutletId { get; set; }

    // 导航属性
    public ExpCostPlanItem Item { get; set; } = null!;
}
