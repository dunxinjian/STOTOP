using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 预付款记录
/// </summary>
public class ExpPrepayment : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>金额</summary>
    public decimal FAmount { get; set; }
    /// <summary>付款日期</summary>
    public DateTime? FPaymentDate { get; set; }
    /// <summary>付款方式</summary>
    public string? FPaymentMethod { get; set; }
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
