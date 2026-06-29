using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfCardRelation : BaseEntity, IOrgScoped
{
    public long FSourceCardId { get; set; }
    public long FTargetCardId { get; set; }
    public string FRelationType { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public decimal? FOffsetAmount { get; set; }
    public string? FSnapshotDataJson { get; set; }
    public DateTime FCreatedTime { get; set; }
    public long FOrgId { get; set; }
}
