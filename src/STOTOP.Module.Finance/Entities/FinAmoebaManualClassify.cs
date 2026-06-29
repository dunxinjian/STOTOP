using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinAmoebaManualClassify : BaseEntity, IOrgScoped
{
    public long FVoucherEntryId { get; set; }   // F凭证分录ID
    public long FPLItemId { get; set; }         // F损益项ID
    public DateTime FMarkedTime { get; set; }   // F标记时间
    public string? FMarkedBy { get; set; }      // F标记人
    public string? FSummaryPattern { get; set; } // F摘要模式
    public long FOrgId { get; set; }            // F组织ID
}
