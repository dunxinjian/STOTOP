using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinVoucherTemplateEntry : BaseEntity
{
    public long FTemplateId { get; set; }
    public string? FSummary { get; set; }
    public long FAccountId { get; set; }
    public string FAccountCode { get; set; } = string.Empty;
    public string FAccountName { get; set; } = string.Empty;
    public decimal FDebitAmount { get; set; }
    public decimal FCreditAmount { get; set; }
    public int FSeq { get; set; }
    public string? FAuxiliaryJson { get; set; }

    public FinVoucherTemplate Template { get; set; } = null!;
}
