using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfFlowGroup : BaseEntity, IOrgScoped
{
    public string FGroupName { get; set; } = string.Empty;
    public string FGroupCode { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public string FStatus { get; set; } = "active";
    public long FOrgId { get; set; }
    public long FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FUpdatedTime { get; set; }
}
