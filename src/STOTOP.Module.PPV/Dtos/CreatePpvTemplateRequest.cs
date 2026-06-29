using System.ComponentModel.DataAnnotations;

namespace STOTOP.Module.PPV.Dtos;

/// <summary>
/// 创建 PPV 产值模板请求
/// </summary>
public class CreatePpvTemplateRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public long PositionId { get; set; }
    [Required]
    public string ItemCode { get; set; } = string.Empty;
    [Required]
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}
