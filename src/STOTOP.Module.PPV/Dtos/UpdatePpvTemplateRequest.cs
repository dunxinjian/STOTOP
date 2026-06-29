using System.ComponentModel.DataAnnotations;

namespace STOTOP.Module.PPV.Dtos;

/// <summary>
/// 更新 PPV 产值模板请求
/// </summary>
public class UpdatePpvTemplateRequest
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
