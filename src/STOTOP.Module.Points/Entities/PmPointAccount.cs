using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmPointAccount : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FUserId { get; set; }
    public int FTotalPoints { get; set; }
    public int FUsedPoints { get; set; }
    public int FAvailablePoints { get; set; }
    public int FMonthlyAward { get; set; }
    public int FMonthlyDeduct { get; set; }
    public int FYearlyPoints { get; set; }
    /// <summary>账户类型：1=A 终身资本 / 2=B 周期清算</summary>
    public int F账户类型 { get; set; } = 1;
    /// <summary>期初余额快照日期（半开区间起点）</summary>
    public DateTime? F期初余额快照日期 { get; set; }
    /// <summary>期初余额快照值</summary>
    public int F期初余额快照值 { get; set; }
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
