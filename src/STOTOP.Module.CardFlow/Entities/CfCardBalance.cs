using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfCardBalance : BaseEntity, IOrgScoped
{
    public long FCardId { get; set; }
    public decimal FOriginalAmount { get; set; }
    public decimal FOffsetAmount { get; set; }
    public decimal FRemainingAmount { get; set; }
    public string FStatus { get; set; } = "active";
    public long FOrgId { get; set; }
    public DateTime? FUpdatedTime { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
