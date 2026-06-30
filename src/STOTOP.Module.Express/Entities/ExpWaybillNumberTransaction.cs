using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 运单号交易
/// </summary>
public class ExpWaybillNumberTransaction : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>品牌ID</summary>
    public string FBrandCode { get; set; } = string.Empty;
    /// <summary>号段ID</summary>
    public long? FPoolId { get; set; }
    /// <summary>交易类型 1分配 2回收</summary>
    public int? FTransactionType { get; set; }
    /// <summary>数量</summary>
    public int FQuantity { get; set; }
    /// <summary>起始号</summary>
    public string? FStartNo { get; set; }
    /// <summary>截止号</summary>
    public string? FEndNo { get; set; }
    /// <summary>交易日期</summary>
    public DateTime? FTransactionDate { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
