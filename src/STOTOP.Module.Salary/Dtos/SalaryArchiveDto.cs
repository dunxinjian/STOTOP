namespace STOTOP.Module.Salary.Dtos;

public class SalaryArchiveDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long EmployeeId { get; set; }
    public long GradeId { get; set; }
    public string? GradeName { get; set; }
    public DateTime EnrollDate { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal PositionAllowance { get; set; }
    public decimal SocialInsuranceBase { get; set; }
    public decimal HousingFundBase { get; set; }
    public decimal TaxThreshold { get; set; }
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class CreateSalaryArchiveRequest
{
    public long EmployeeId { get; set; }
    public long GradeId { get; set; }
    public DateTime EnrollDate { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal PositionAllowance { get; set; }
    public decimal SocialInsuranceBase { get; set; }
    public decimal HousingFundBase { get; set; }
    public decimal TaxThreshold { get; set; } = 5000m;
    public string? Remark { get; set; }
}

public class UpdateSalaryArchiveRequest
{
    public long GradeId { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal PositionAllowance { get; set; }
    public decimal SocialInsuranceBase { get; set; }
    public decimal HousingFundBase { get; set; }
    public decimal TaxThreshold { get; set; } = 5000m;
    public string? Remark { get; set; }
}
