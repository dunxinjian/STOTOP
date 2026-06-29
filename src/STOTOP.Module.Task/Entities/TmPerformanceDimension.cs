using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmPerformanceDimension : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string FDimensionName { get; set; } = string.Empty;
    public string FDimensionCode { get; set; } = string.Empty;
    public int FDataSource { get; set; }
    public int FWeight { get; set; } = 100;
    public decimal FMaxScore { get; set; } = 100;
    public int FSort { get; set; } = 0;
    public bool FIsEnabled { get; set; } = true;

    // 导航属性
    public virtual ICollection<TmPerformanceScore> Scores { get; set; } = new List<TmPerformanceScore>();
}
