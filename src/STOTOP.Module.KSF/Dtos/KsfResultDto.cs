namespace STOTOP.Module.KSF.Dtos;

/// <summary>
/// KSF 核算结果 DTO
/// </summary>
public class KsfResultDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string Period { get; set; } = string.Empty;
    public long PlanId { get; set; }
    public string? PlanName { get; set; }
    public long PositionIdSnapshot { get; set; }
    public long DepartmentIdSnapshot { get; set; }
    public long? BusinessUnitIdSnapshot { get; set; }
    public decimal FixedPart { get; set; }
    public decimal FloatingPart { get; set; }
    public decimal Raise { get; set; }
    public decimal Deduction { get; set; }
    public decimal NetPayout { get; set; }
    public string? PlanSnapshotJson { get; set; }
    /// <summary>状态：1=试运行 2=正式 3=取数异常</summary>
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public List<KsfResultDetailDto> Details { get; set; } = new();
}
