using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 月度调整
/// </summary>
public class ExpMonthlyAdjustment : BaseEntity, IOrgScoped, ITenantScoped
{
    /// <summary>业务对象ID（F编号）</summary>
    public string FBusinessObjectId { get; set; } = string.Empty;
    /// <summary>品牌ID</summary>
    public string? FBrandCode { get; set; }
    /// <summary>月份（当月第一天）</summary>
    public DateTime FMonth { get; set; }
    /// <summary>调整类型 1均重超标 2占比超标</summary>
    public int FAdjustType { get; set; }
    /// <summary>原因</summary>
    public string? FReason { get; set; }
    /// <summary>金额</summary>
    public decimal FAmount { get; set; }
    /// <summary>状态 0待确认 1已确认 2已纳入账单</summary>
    public int FStatus { get; set; }
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
