using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 政策返利阶梯档位
/// </summary>
public class ExpPolicyRebateTier : BaseEntity
{
    /// <summary>政策返利ID</summary>
    public long FPolicyRebateId { get; set; }
    /// <summary>日单量起</summary>
    public int FDailyVolumeFrom { get; set; }
    /// <summary>日单量止</summary>
    public int? FDailyVolumeTo { get; set; }
    /// <summary>每票返利</summary>
    public decimal FRebatePerTicket { get; set; }
    /// <summary>排序</summary>
    public int FSortOrder { get; set; }
}
