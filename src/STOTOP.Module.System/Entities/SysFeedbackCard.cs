using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysFeedbackCard : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public string FTitle { get; set; } = string.Empty;
    public int FType { get; set; }
    public string FModule { get; set; } = string.Empty;
    public int FSeverity { get; set; } = 2;
    public int FStatus { get; set; } = 0;
    public long FSubmitterId { get; set; }
    public long? FAssigneeId { get; set; }
    public string? FDescription { get; set; }
    public string? FReproduceSteps { get; set; }
    public string? FExpectedResult { get; set; }
    public string? FActualResult { get; set; }
    public string? FAttachmentLinks { get; set; }
    public string? FPageUrl { get; set; }
    public string? FClientInfo { get; set; }
    public string? FVersion { get; set; }
    public string? FConclusion { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
    public DateTime? FClosedTime { get; set; }
}
