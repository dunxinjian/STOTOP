using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmVisitRecord : BaseEntity, IOrgScoped
{
    public string FCustomerId { get; set; } = string.Empty;
    public long FVisitorId { get; set; }
    public DateOnly FVisitDate { get; set; }
    public int FVisitMethod { get; set; }
    public string? FContent { get; set; }
    public DateOnly? FNextFollowUpDate { get; set; }
    public long FOrgId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmCustomer Customer { get; set; } = null!;
}
