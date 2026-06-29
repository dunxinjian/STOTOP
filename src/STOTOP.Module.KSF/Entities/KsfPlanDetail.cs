using STOTOP.Core.Models;

namespace STOTOP.Module.KSF.Entities;

/// <summary>
/// KSF 岗位方案明细
/// </summary>
public class KsfPlanDetail : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    /// <summary>FK → KsfPlan.FID</summary>
    public long F方案ID { get; set; }
    /// <summary>FK → KsfIndicator.FID</summary>
    public long F指标ID { get; set; }
    /// <summary>权重（百分比，如 30.00）</summary>
    public decimal F权重 { get; set; }
    /// <summary>指标平衡值</summary>
    public decimal F平衡点 { get; set; }
    /// <summary>阶梯激励配置 JSON</summary>
    public string? F激励刻度JSON { get; set; }
    /// <summary>金额变动下限护栏</summary>
    public decimal F最低保底 { get; set; }
}
