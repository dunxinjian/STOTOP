using STOTOP.Core.Models;

namespace STOTOP.Module.Supplier.Entities;

public class SupSupplier : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public string FCode { get; set; } = string.Empty;
    /// <summary>全称</summary>
    public string FFullName { get; set; } = string.Empty;
    public string? FShortName { get; set; }
    public string? FCreditCode { get; set; }
    public string? FTaxNumber { get; set; }
    public string? FContact { get; set; }
    public string? FPhone { get; set; }
    public string? FEmail { get; set; }
    public string? FAddress { get; set; }
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 1;
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public List<SupBankAccount> BankAccounts { get; set; } = new();
}
