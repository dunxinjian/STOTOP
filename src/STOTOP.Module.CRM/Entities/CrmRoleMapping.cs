using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmRoleMapping : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEmployeeId { get; set; }
    public int FRole { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }
}
