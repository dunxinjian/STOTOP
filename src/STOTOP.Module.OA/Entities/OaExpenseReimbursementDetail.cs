using STOTOP.Core.Models;

namespace STOTOP.Module.OA.Entities;

public class OaExpenseReimbursementDetail : BaseEntity
{
    public long FReimbursementId { get; set; }
    public int FLineNo { get; set; }
    public string FExpenseType { get; set; } = string.Empty;
    public string? FExpenseAccountCode { get; set; }
    public string FSummary { get; set; } = string.Empty;
    public decimal FAmount { get; set; }
    public DateOnly FOccurDate { get; set; }
    public string? FRemark { get; set; }
}
