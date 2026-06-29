using STOTOP.Core.Models;

namespace STOTOP.Module.Workflow.Entities;

/// <summary>WF工作项日志 - 记录状态变更和操作历史</summary>
public class WfWorkItemLog : BaseEntity
{
    public long FWorkItemId { get; set; }
    public long FOperatorId { get; set; }
    public string? FOperatorName { get; set; }
    public string FAction { get; set; } = string.Empty;
    public int? FFromStatus { get; set; }
    public int? FToStatus { get; set; }
    public string? FContent { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual WfWorkItem? WorkItem { get; set; }
}
