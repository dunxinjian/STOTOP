using System.ComponentModel.DataAnnotations;

namespace STOTOP.Module.PPV.Dtos;

/// <summary>
/// 创建 PPV 产值记录请求
/// </summary>
public class CreatePpvRecordRequest
{
    [Required]
    public long EmployeeId { get; set; }
    [Required]
    public string Period { get; set; } = string.Empty;
    [Required]
    public long TemplateId { get; set; }
    public decimal Quantity { get; set; }
    public int QualityLevel { get; set; } = 1;
    public bool IsCrossPosition { get; set; }
}
