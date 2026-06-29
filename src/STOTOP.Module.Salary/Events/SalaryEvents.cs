using STOTOP.Infrastructure.Events;

namespace STOTOP.Module.Salary.Events;

/// <summary>
/// 工资单发放完成事件
/// </summary>
public class SalaryReleasedEvent : BusinessEvent
{
    public long EmployeeId { get; set; }
    public string Period { get; set; } = "";
    public long PayrollId { get; set; }
    public decimal NetAmount { get; set; }
}

/// <summary>
/// 晋升触发事件 — 由 PromotionScanJob 发现员工满足晋升条件时发布
/// </summary>
public class PromotionTriggeredEvent : BusinessEvent
{
    public long EmployeeId { get; set; }
    public long ReviewId { get; set; }
    public long RuleId { get; set; }
    public string CurrentGrade { get; set; } = "";
    public string TargetGrade { get; set; } = "";
    public int AScoreSnapshot { get; set; }
}
