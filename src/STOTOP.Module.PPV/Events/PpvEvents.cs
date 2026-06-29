using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.PPV.Events;

/// <summary>
/// 产值记录审核通过时发布（F审核状态 0→1）
/// </summary>
public class PpvWorkRecordedEvent : BusinessEvent
{
    public long EmployeeId { get; set; }
    public string Period { get; set; } = "";
    public long RecordId { get; set; }
    public decimal ProductValue { get; set; }
    public int QualityGrade { get; set; }  // 1=A 2=B 3=C 4=D
    public bool IsCrossPosition { get; set; }
}

/// <summary>
/// 月度汇总完成后发布（供 Points/Salary 等订阅消费）
/// </summary>
public class PpvMonthlyAggregatedEvent : BusinessEvent
{
    public long EmployeeId { get; set; }
    public string Period { get; set; } = "";
    public long MonthlyResultId { get; set; }
    public decimal TotalProductValue { get; set; }
    public int OverallQualityGrade { get; set; }
    public bool IsCrossPositionCleared { get; set; }
    public int BScoreChange { get; set; }
    public int AScoreChange { get; set; }
}
