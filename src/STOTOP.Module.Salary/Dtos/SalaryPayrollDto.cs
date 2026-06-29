namespace STOTOP.Module.Salary.Dtos;

public class SalaryPayrollDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long EmployeeId { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal BaseSalary { get; set; }
    public decimal KsfFloat { get; set; }
    public decimal PpvBonus { get; set; }
    public decimal BPointExchange { get; set; }
    public decimal AttendanceDeduction { get; set; }
    public decimal SocialInsurancePersonal { get; set; }
    public decimal HousingFundPersonal { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal GrossTotal { get; set; }
    public decimal NetTotal { get; set; }
    public int Status { get; set; }
    public long? AuditorId { get; set; }
    public DateTime? AuditTime { get; set; }
    public DateTime? ReleaseTime { get; set; }
    public DateTime CreateTime { get; set; }
    public List<SalaryPayrollDetailDto> Details { get; set; } = new();
}

public class SalaryPayrollDetailDto
{
    public long Id { get; set; }
    public long PayrollId { get; set; }
    public int ItemType { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public long? SourceId { get; set; }
    public string? SourceType { get; set; }
    public string? Remark { get; set; }
}

public class RecalcPayrollRequest
{
    public string Period { get; set; } = string.Empty;
    public List<long>? EmployeeIds { get; set; }
}
