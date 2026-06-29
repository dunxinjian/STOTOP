using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfFlowVersion : BaseEntity
{
    public long FFlowDefinitionId { get; set; }
    public int FVersionNumber { get; set; }
    public string FStatus { get; set; } = "draft";
    public string? FCardSchemaJson { get; set; }
    public string? FDetailSchemaJson { get; set; }
    public string? FFlowSettingsJson { get; set; }
    public long FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FPublishTime { get; set; }
    public bool FIsCurrentVersion { get; set; }
}
