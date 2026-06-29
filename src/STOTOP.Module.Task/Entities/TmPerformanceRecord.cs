using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmPerformanceRecord : BaseEntity, IOrgScoped
{
    public long FPeriodId { get; set; }
    public long FEmployeeId { get; set; }
    public long FOrgId { get; set; }
    public int FTaskTotal { get; set; } = 0;
    public int FCompletedCount { get; set; } = 0;
    public int FOnTimeCount { get; set; } = 0;
    public int FOverdueCount { get; set; } = 0;
    public decimal FCompletionRate { get; set; } = 0;
    public decimal FOnTimeRate { get; set; } = 0;
    public decimal FGoalAchievementRate { get; set; } = 0;
    public decimal? FQualityScore { get; set; }
    public decimal? FSelfScore { get; set; }
    public decimal? FOverallScore { get; set; }
    public string? FGrade { get; set; }
    public string? FComment { get; set; }
    public string? FSelfComment { get; set; }
    public int FStatus { get; set; } = 0;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmPerformancePeriod? Period { get; set; }
    public virtual ICollection<TmPerformanceScore> Scores { get; set; } = new List<TmPerformanceScore>();
}
