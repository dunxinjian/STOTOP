using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmServiceOrderLog : BaseEntity, IOrgScoped
{
    public long FOrderId { get; set; }
    public long FOperatorId { get; set; }
    public int FOperationType { get; set; }
    public string? FContent { get; set; }
    public string? FAttachments { get; set; }
    public long FOrgId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmServiceOrder Order { get; set; } = null!;
}
