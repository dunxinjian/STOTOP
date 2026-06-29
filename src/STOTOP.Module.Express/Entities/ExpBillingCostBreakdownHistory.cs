using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 计费成本明细历史（归档）
/// </summary>
public class ExpBillingCostBreakdownHistory : BaseEntity, IOrgScoped
{
    /// <summary>计费结果ID</summary>
    public long FBillingResultId { get; set; }
    /// <summary>成本项目ID</summary>
    public int FCostItemId { get; set; }
    /// <summary>金额</summary>
    public decimal FAmount { get; set; }
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>归档时间</summary>
    public DateTime FArchivedAt { get; set; } = DateTime.Now;
}
