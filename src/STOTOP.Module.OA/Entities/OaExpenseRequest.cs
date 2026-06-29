using STOTOP.Core.Models;

namespace STOTOP.Module.OA.Entities;

public class OaExpenseRequest : BaseEntity
{
    public string FDocNumber { get; set; } = string.Empty;
    public long FApplicantId { get; set; }
    public long FDeptId { get; set; }
    public long FOrgId { get; set; }
    public string FReason { get; set; } = string.Empty;
    public decimal FAmount { get; set; }
    public DateOnly? FExpectedPayDate { get; set; }
    public string FExpenseType { get; set; } = string.Empty;
    public string? FPayeeName { get; set; }
    public string? FPayeeAccount { get; set; }
    public string? FPayeeBank { get; set; }
    public string? FRemark { get; set; }
    public int FDocStatus { get; set; }
    public decimal FReferencedAmount { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FModifiedTime { get; set; }
}
