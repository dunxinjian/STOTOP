using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmPerformanceDimension : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
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
