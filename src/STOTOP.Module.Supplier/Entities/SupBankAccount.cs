using STOTOP.Core.Models;

namespace STOTOP.Module.Supplier.Entities;

public class SupBankAccount : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FSupplierId { get; set; }
    public string FAccountName { get; set; } = string.Empty;
    public string FBankName { get; set; } = string.Empty;
    public string FBankAccountNumber { get; set; } = string.Empty;
    public string? FBranchName { get; set; }
    public bool FIsDefault { get; set; } = false;
    public int FStatus { get; set; } = 1;
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public SupSupplier Supplier { get; set; } = null!;
}
