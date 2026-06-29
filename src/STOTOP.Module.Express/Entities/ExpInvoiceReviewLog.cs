using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Entities;

/// <summary>
/// 账单审核日志
/// </summary>
public class ExpInvoiceReviewLog : BaseEntity, IOrgScoped
{
    /// <summary>账单ID</summary>
    public long FInvoiceId { get; set; }
    /// <summary>操作 1自动通过 2自动驳回 3人工通过 4人工驳回 5反审核</summary>
    public int FAction { get; set; }
    /// <summary>规则ID</summary>
    public long? FRuleId { get; set; }
    /// <summary>规则结果</summary>
    public string? FRuleResult { get; set; }
    /// <summary>操作人ID</summary>
    public long? FOperatorId { get; set; }
    /// <summary>备注</summary>
    public string? FRemark { get; set; }
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>创建时间</summary>
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
}
