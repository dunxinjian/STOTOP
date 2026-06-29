namespace STOTOP.Module.KSF.Dtos;

/// <summary>
/// KSF 核算结果明细 DTO
/// </summary>
public class KsfResultDetailDto
{
    public long Id { get; set; }
    public long ResultId { get; set; }
    public long IndicatorId { get; set; }
    public string? IndicatorCode { get; set; }
    public string? IndicatorName { get; set; }
    public string? IndicatorUnit { get; set; }
    public decimal ActualValue { get; set; }
    public decimal Diff { get; set; }
    public decimal AmountChange { get; set; }
    public string? IndicatorSnapshotJson { get; set; }
}
