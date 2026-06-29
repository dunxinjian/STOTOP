using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmServiceOrder : BaseEntity, IOrgScoped
{
    public string FOrderNo { get; set; } = string.Empty;
    public string FCustomerId { get; set; } = string.Empty;
    public long? FAssigneeId { get; set; }
    public int FCategory { get; set; }
    public int FPriority { get; set; } = 3;
    public string FTitle { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public int FStatus { get; set; } = 0;
    public DateTime? FResolvedTime { get; set; }
    public long FOrgId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmCustomer Customer { get; set; } = null!;
    public List<CrmServiceOrderLog> Logs { get; set; } = new();
}
