namespace STOTOP.Module.PPV.Dtos;

/// <summary>
/// PPV 产值记录 DTO
/// </summary>
public class PpvRecordDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string Period { get; set; } = string.Empty;
    public long TemplateId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public int QualityLevel { get; set; }
    public bool IsCrossPosition { get; set; }
    public int ReviewStatus { get; set; }
    public long? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewTime { get; set; }
    public string? ReviewRemark { get; set; }
    public DateTime CreateTime { get; set; }
}
