namespace STOTOP.Module.PPV.Dtos;

/// <summary>
/// PPV 产值模板 DTO
/// </summary>
public class PpvTemplateDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long PositionId { get; set; }
    public string? PositionName { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}
