using STOTOP.Core.Models;

namespace STOTOP.Module.OA.Entities;

public class OaExpenseReimbursement : BaseEntity, IOrgScoped
{
    public string FDocNumber { get; set; } = string.Empty;
    public long FApplicantId { get; set; }
    public long FDeptId { get; set; }
    public long FOrgId { get; set; }
    public string FReason { get; set; } = string.Empty;
    public decimal FTotalAmount { get; set; }
    public long? FRequestRefId { get; set; }
    public long? FLoanRefId { get; set; }
    public string FPaymentMethod { get; set; } = string.Empty;
    public string FPayeeName { get; set; } = string.Empty;
    public string FPayeeAccount { get; set; } = string.Empty;
    public string? FPayeeBank { get; set; }
    public int FAttachmentCount { get; set; }
    public string? FRemark { get; set; }
    public int FDocStatus { get; set; }
    public long? FVoucherId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FModifiedTime { get; set; }

    public virtual ICollection<OaExpenseReimbursementDetail> Details { get; set; } = new List<OaExpenseReimbursementDetail>();
}
