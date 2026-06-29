namespace STOTOP.Module.Salary.Dtos;

public class SalaryGradeDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public string GradeCode { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public int Level { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal PositionAllowance { get; set; }
    public decimal PerformanceBase { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class CreateSalaryGradeRequest
{
    public string GradeCode { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public int Level { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal PositionAllowance { get; set; }
    public decimal PerformanceBase { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class UpdateSalaryGradeRequest
{
    public string GradeName { get; set; } = string.Empty;
    public int Level { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal PositionAllowance { get; set; }
    public decimal PerformanceBase { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public bool IsEnabled { get; set; } = true;
}
