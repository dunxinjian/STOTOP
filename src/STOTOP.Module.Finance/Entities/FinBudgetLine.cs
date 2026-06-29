using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinBudgetLine : BaseEntity, IOrgScoped
{
    public long FBudgetVersionId { get; set; }
    public string FPeriod { get; set; } = string.Empty;
    public long FOrgId { get; set; }
    public long? FAmoebaUnitId { get; set; }
    public long? FAccountId { get; set; }
    public string? FAccountCode { get; set; }
    public long? FPLItemId { get; set; }
    public string? FDimensionJson { get; set; }
    public decimal FAmount { get; set; }
    public decimal? FQuantity { get; set; }
    public decimal? FUnitPrice { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
