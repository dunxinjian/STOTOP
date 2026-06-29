using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmRedeemRecord : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FUserId { get; set; }
    public long FItemId { get; set; }
    public int FDeductedPoints { get; set; }
    public int FStatus { get; set; }
    public long? FIssuerId { get; set; }
    public DateTime? FIssueTime { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
