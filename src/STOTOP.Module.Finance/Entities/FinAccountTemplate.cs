using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAccountTemplate : BaseEntity
{
    public string FCode { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public int FIsPreset { get; set; }
    public int FEnableStatus { get; set; } = 1;
    public long FOrgId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
