namespace STOTOP.Module.KSF.Dtos;

/// <summary>
/// KSF 员工 → 经营单元映射 DTO
/// </summary>
public class KsfMappingDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public long BusinessUnitId { get; set; }
    public string? BusinessUnitName { get; set; }
    public decimal Ratio { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime CreateTime { get; set; }
}
