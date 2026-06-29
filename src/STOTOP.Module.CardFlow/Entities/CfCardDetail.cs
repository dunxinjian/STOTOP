using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfCardDetail : BaseEntity
{
    public long FCardId { get; set; }
    public string FDetailTableKey { get; set; } = "default";
    public int FSortOrder { get; set; }
    public string? FDataJson { get; set; }
    public DateTime FCreatedTime { get; set; }
}
