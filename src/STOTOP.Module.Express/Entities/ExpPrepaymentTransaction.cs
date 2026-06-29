using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 预付款流水
/// </summary>
public class ExpPrepaymentTransaction : BaseEntity, IOrgScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>交易类型 1充值 2核销 3退款</summary>
    public int FTransactionType { get; set; }
    /// <summary>金额</summary>
    public decimal FAmount { get; set; }
    /// <summary>账单ID</summary>
    public long? FInvoiceId { get; set; }
    /// <summary>交易后余额</summary>
    public decimal? FBalanceAfter { get; set; }
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
