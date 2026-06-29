using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmPerformancePeriod : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public string FName { get; set; } = string.Empty;
    public long FOrgId { get; set; }
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
