using STOTOP.Core.Models;

namespace STOTOP.Module.KSF.Entities;

/// <summary>
/// KSF 结果明细
/// </summary>
public class KsfResultDetail : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    /// <summary>FK → KsfResult.FID</summary>
    public long F结果ID { get; set; }
    public long F指标ID { get; set; }
    public decimal F实际值 { get; set; }
    /// <summary>实际值 - 平衡点</summary>
    public decimal F差额 { get; set; }
    /// <summary>按阶梯计算后的金额变动</summary>
    public decimal F金额变动 { get; set; }
    /// <summary>指标快照 JSON</summary>
    public string? F指标快照JSON { get; set; }
}
