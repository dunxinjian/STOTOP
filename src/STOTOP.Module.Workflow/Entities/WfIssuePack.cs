using STOTOP.Core.Models;

namespace STOTOP.Module.Workflow.Entities;

/// <summary>WF问题包 - 按批次+类型聚合的问题集合</summary>
public class WfIssuePack : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public string? FChainId { get; set; }
    public long? FWorkItemId { get; set; }
    public string FIssueType { get; set; } = string.Empty;
    public long? FBatchId { get; set; }
    public int FTotalCount { get; set; } = 0;
    public int FResolvedCount { get; set; } = 0;
    public string? FSummary { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<WfIssueDetail> Details { get; set; } = new List<WfIssueDetail>();
    public virtual WfWorkItem? WorkItem { get; set; }
}
