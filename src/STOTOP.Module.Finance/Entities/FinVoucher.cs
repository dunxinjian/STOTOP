using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinVoucher : BaseEntity, IOrgScoped
{
    public string FVoucherWord { get; set; } = string.Empty;
    public int FVoucherNo { get; set; }
    public DateTime FDate { get; set; }
    public long FPeriodId { get; set; }
    public int FAttachmentCount { get; set; }
    public string FCreator { get; set; } = string.Empty;
    public string? FAuditor { get; set; }
    public string? FModifier { get; set; }
    public int FStatus { get; set; }
    public string? FSource { get; set; }
    public string? FRemark { get; set; }
    public long FAccountSetId { get; set; }
    public long FOrgId { get; set; }  // 组织ID
    public string? FDataScopeId { get; set; }   // 数据血缘标记（批次ID等）
    public bool FIsRevoked { get; set; }           // 是否已被撤销/红冲
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
    
    public List<FinVoucherEntry> Entries { get; set; } = new();
}
