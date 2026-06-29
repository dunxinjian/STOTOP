using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAccountTemplateItem : BaseEntity
{
    public long FTemplateId { get; set; }
    public string FCode { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string FCategory { get; set; } = string.Empty;
    public string FBalanceDirection { get; set; } = string.Empty;
    public int FLevel { get; set; }
    public long FParentId { get; set; }
    public int FIsLeaf { get; set; }
    public string? FAuxiliary { get; set; }
    public string? FCurrency { get; set; }
    public string? FUnit { get; set; }
    public int FSortOrder { get; set; }
}
