using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinBudgetExpenseMapping : BaseEntity
{
    public long FAccountSetId { get; set; }
    public long? FOrgId { get; set; }
    public string FExpenseType { get; set; } = string.Empty;
    public string? FAccountCode { get; set; }
    public long? FPLItemId { get; set; }
    public string FCashCategory { get; set; } = "expense_reimbursement";
    public int FStatus { get; set; } = 1;
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
