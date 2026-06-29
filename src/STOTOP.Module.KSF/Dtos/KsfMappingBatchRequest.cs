using System.ComponentModel.DataAnnotations;

namespace STOTOP.Module.KSF.Dtos;

/// <summary>
/// KSF 员工映射批量保存请求项
/// </summary>
public class KsfMappingBatchRequest
{
    public long? Id { get; set; }
    [Required]
    public long EmployeeId { get; set; }
    [Required]
    public long BusinessUnitId { get; set; }
    public decimal Ratio { get; set; } = 1.0m;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}
