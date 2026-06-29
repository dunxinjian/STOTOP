using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 成本方案_成本项
/// </summary>
public class ExpCostPlanItem : BaseEntity
{
    /// <summary>方案ID</summary>
    public long FPlanId { get; set; }
    /// <summary>成本项名称</summary>
    public string FItemName { get; set; } = string.Empty;
    /// <summary>成本项类型 1=全国单价 2=省份矩阵 3=城市加收 4=一口价</summary>
    public int FItemType { get; set; } = 1;
    /// <summary>结算重量环节（仅一口价使用）1=揽收 2=中转 3=到件 4=集包 5=计泡 6=总部 7=取最大值</summary>
    public int? FSettlementWeightStage { get; set; }
    /// <summary>排序号</summary>
    public int FSortOrder { get; set; } = 0;

    // 导航属性
    public ExpCostPlan Plan { get; set; } = null!;
    public List<ExpCostPlanItemOutlet> Outlets { get; set; } = new();
    public List<ExpCostPlanItemShop> Shops { get; set; } = new();
    public List<ExpCostPlanItemPeriod> Periods { get; set; } = new();
}
