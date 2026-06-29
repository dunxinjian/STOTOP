using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmServiceFeedback : BaseEntity, IOrgScoped
{
    public long FSubmitterId { get; set; }
    public long FOrgId { get; set; }
    public string? FCustomerId { get; set; }
    public long? FOrderId { get; set; }
    public int FCategory { get; set; }
    public string FTitle { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public string? FSuggestion { get; set; }
    public string? FAttachments { get; set; }
    public int FStatus { get; set; } = 0;
    public long? FHandlerId { get; set; }
    public string? FHandleResult { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }
}
