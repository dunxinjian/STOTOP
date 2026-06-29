namespace STOTOP.Module.PPV.Dtos;

/// <summary>
/// 更新 PPV 产值记录请求
/// </summary>
public class UpdatePpvRecordRequest
{
    public decimal Quantity { get; set; }
    public int QualityLevel { get; set; }
    public bool IsCrossPosition { get; set; }
}
