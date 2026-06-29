namespace STOTOP.Module.PPV.Dtos;

/// <summary>
/// PPV 月度汇总 DTO
/// </summary>
public class PpvMonthlyResultDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal OwnPositionAmount { get; set; }
    public decimal CrossPositionAmount { get; set; }
    public int ComprehensiveQualityLevel { get; set; }
    public bool IsCrossPositionCleared { get; set; }
    public string? ClearReason { get; set; }
    public int BScoreChange { get; set; }
    public int AScoreChange { get; set; }
    public long PositionIdSnapshot { get; set; }
    public long DepartmentIdSnapshot { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
}
