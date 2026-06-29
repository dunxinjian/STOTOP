namespace STOTOP.Module.KSF.Dtos;

/// <summary>
/// KSF 方案明细 DTO
/// </summary>
public class KsfPlanDetailDto
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public long IndicatorId { get; set; }
    public string? IndicatorCode { get; set; }
    public string? IndicatorName { get; set; }
    public string? IndicatorUnit { get; set; }
    public decimal Weight { get; set; }
    public decimal Balance { get; set; }
    public string? IncentiveScaleJson { get; set; }
    public decimal MinGuarantee { get; set; }
}
