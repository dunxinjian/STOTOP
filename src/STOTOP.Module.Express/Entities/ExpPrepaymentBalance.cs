using System.ComponentModel.DataAnnotations;
using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 预付款余额
/// </summary>
public class ExpPrepaymentBalance : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>余额</summary>
    public decimal FBalance { get; set; }
    /// <summary>累计充值</summary>
    public decimal FTotalRecharge { get; set; }
    /// <summary>累计消费</summary>
    public decimal FTotalConsume { get; set; }
    /// <summary>更新时间</summary>
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;
    /// <summary>并发控制（乐观锁）</summary>
    [Timestamp]
    public byte[]? FRowVersion { get; set; }
}
