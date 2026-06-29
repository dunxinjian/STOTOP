using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

/// <summary>
/// FIN交易渠道
/// </summary>
public class FinPaymentChannel : BaseEntity
{
    public string FName { get; set; } = string.Empty;
    public int FType { get; set; }
    public string? FAccountNo { get; set; }
    public string? FBankName { get; set; }
    public string? FImportTemplate { get; set; }
    public int FStatus { get; set; } = 1;
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; }
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }
}
