using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinVoucherEntry : BaseEntity, IOrgScoped
{
    public long FVoucherId { get; set; }
    public int FLineNo { get; set; }
    public string FSummary { get; set; } = string.Empty;
    public long FAccountId { get; set; }
    public string FAccountCode { get; set; } = string.Empty;
    public string FAccountName { get; set; } = string.Empty;
    public string? FAuxiliaryJson { get; set; }
    public decimal FDebitAmount { get; set; }
    public decimal FCreditAmount { get; set; }
    public string? FCurrencyCode { get; set; }    // 币种代码，null 表示本位币
    public decimal? FExchangeRate { get; set; }   // 汇率
    public decimal? FOriginalAmount { get; set; } // 原币金额
    public long FOrgId { get; set; }  // 组织ID
    public string? FDataScopeId { get; set; }   // 数据血缘标记
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }

    public FinVoucher Voucher { get; set; } = null!;
}
