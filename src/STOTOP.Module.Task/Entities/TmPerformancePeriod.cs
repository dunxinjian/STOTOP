using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmPerformancePeriod : BaseEntity, IOrgScoped, ITenantScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public string FName { get; set; } = string.Empty;
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public int FType { get; set; }
    public DateTime FStartDate { get; set; }
    public DateTime FEndDate { get; set; }
    public int FStatus { get; set; } = 0;
    public long FCreatorId { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<TmPerformanceRecord> Records { get; set; } = new List<TmPerformanceRecord>();
}
