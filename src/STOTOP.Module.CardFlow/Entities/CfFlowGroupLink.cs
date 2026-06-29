using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfFlowGroupLink : BaseEntity
{
    public long FFlowGroupId { get; set; }
    public long FSourceFlowId { get; set; }
    public long FTargetFlowId { get; set; }
    public string? FTriggerCondition { get; set; }
    public string? FFieldMappingJson { get; set; }
    public string FTriggerMode { get; set; } = "auto";
    public int FSortOrder { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FUpdatedTime { get; set; }
}
