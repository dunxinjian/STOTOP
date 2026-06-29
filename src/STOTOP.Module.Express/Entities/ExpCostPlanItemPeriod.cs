using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 成本方案_成本项_时间段（时间链）
/// </summary>
public class ExpCostPlanItemPeriod : BaseEntity
{
    /// <summary>成本项ID</summary>
    public long FItemId { get; set; }
    /// <summary>生效日期</summary>
    public DateTime FEffectiveDate { get; set; }
    /// <summary>矩阵JSON（该时段的完整报价矩阵，含重量段+目的省份）</summary>
    public string? FMatrixJson { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // 导航属性
    public ExpCostPlanItem Item { get; set; } = null!;
}
