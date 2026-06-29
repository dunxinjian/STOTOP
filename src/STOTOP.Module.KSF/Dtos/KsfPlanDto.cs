namespace STOTOP.Module.KSF.Dtos;

/// <summary>
/// KSF 岗位方案 DTO
/// </summary>
public class KsfPlanDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long PositionId { get; set; }
    public string? PositionName { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsEnabled { get; set; }
    /// <summary>运行模式：0=试运行 1=正式</summary>
    public int RunMode { get; set; }
    public string? ThresholdRulesJson { get; set; }
    public decimal MonthlyRaiseBase { get; set; }
    public long OwnerId { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<KsfPlanDetailDto> Details { get; set; } = new();
}
