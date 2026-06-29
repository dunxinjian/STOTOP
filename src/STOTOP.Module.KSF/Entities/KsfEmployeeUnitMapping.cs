using STOTOP.Core.Models;

namespace STOTOP.Module.KSF.Entities;

/// <summary>
/// KSF 员工经营单元映射
/// </summary>
public class KsfEmployeeUnitMapping : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long F员工ID { get; set; }
    public long F经营单元ID { get; set; }
    /// <summary>分摊比例（默认 100%）</summary>
    public decimal F分摊比例 { get; set; } = 1.0m;
    public DateTime F生效起期 { get; set; }
    public DateTime? F生效止期 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
