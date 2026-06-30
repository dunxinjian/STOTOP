using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 计费成本明细历史（归档）
/// </summary>
public class ExpBillingCostBreakdownHistory : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>计费结果ID</summary>
    public long FBillingResultId { get; set; }
    /// <summary>成本项目ID</summary>
    public int FCostItemId { get; set; }
    /// <summary>金额</summary>
    public decimal FAmount { get; set; }
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>归档时间</summary>
    public DateTime FArchivedAt { get; set; } = DateTime.Now;
}
