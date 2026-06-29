using System.ComponentModel.DataAnnotations;

namespace STOTOP.Module.KSF.Dtos;

/// <summary>
/// KSF 方案明细请求项
/// </summary>
public class KsfPlanDetailRequest
{
    public long? Id { get; set; }
    [Required]
    public long IndicatorId { get; set; }
    public decimal Weight { get; set; }
    public decimal Balance { get; set; }
    public string? IncentiveScaleJson { get; set; }
    public decimal MinGuarantee { get; set; }
}

/// <summary>
/// KSF 岗位方案创建/更新请求
/// </summary>
public class KsfPlanCreateRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public long PositionId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsEnabled { get; set; } = true;
    /// <summary>运行模式：0=试运行 1=正式</summary>
    public int RunMode { get; set; } = 0;
    public string? ThresholdRulesJson { get; set; }
    public decimal MonthlyRaiseBase { get; set; }
    public long OwnerId { get; set; }
    public List<KsfPlanDetailRequest> Details { get; set; } = new();
}
