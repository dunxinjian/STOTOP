using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmPerformanceScore : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FRecordId { get; set; }
    public long FDimensionId { get; set; }
    public decimal? FScore { get; set; }
    public string? FEvaluator { get; set; }
    public string? FRemark { get; set; }

    // 导航属性
    public virtual TmPerformanceRecord? Record { get; set; }
    public virtual TmPerformanceDimension? Dimension { get; set; }
}
