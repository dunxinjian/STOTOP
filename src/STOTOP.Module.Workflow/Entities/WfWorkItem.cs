using STOTOP.Core.Models;
using STOTOP.Module.Workflow.Enums;

namespace STOTOP.Module.Workflow.Entities;

/// <summary>WF工作项 - 事件/任务协调框架核心实体</summary>
public class WfWorkItem : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }

    // 基础信息
    public string FTitle { get; set; } = string.Empty;
    public string? FDescription { get; set; }
    public int FType { get; set; } = (int)WorkItemType.Task;
    public int FSource { get; set; } = (int)WorkItemSource.Pipeline;
    public int FStatus { get; set; } = (int)WorkItemStatus.Pending;
    public int FPriority { get; set; } = (int)WorkItemPriority.Normal;

    // 链路关联
    public string? FChainId { get; set; }
    public int FChainSeq { get; set; } = 0;
    public long? FParentWorkItemId { get; set; }

    // 数据作用域
    public string? FDataScopeId { get; set; }

    // 人员
    public long FCreatorId { get; set; }
    public long? FAssigneeId { get; set; }
    public string? FAssigneeName { get; set; }

    // 业务上下文
    public string? FModule { get; set; }
    public string? FBizType { get; set; }
    public long? FBizId { get; set; }
    public string? FDetailRoute { get; set; }

    // 时间
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
    public DateTime? FDeadline { get; set; }
    public DateTime? FCompletedTime { get; set; }
    public DateTime? FResolvedAt { get; set; }

    // 分类与超时
    public string? FCategory { get; set; }
    public bool FIsOverdue { get; set; }
    public DateTime? FClaimedAt { get; set; }
    public string? FDispatchWarning { get; set; }

    // 处理结果
    public string? FResult { get; set; }
    public string? FRemark { get; set; }

    // 导航属性
    public virtual WfWorkItem? ParentWorkItem { get; set; }
    public virtual ICollection<WfWorkItem> ChildWorkItems { get; set; } = new List<WfWorkItem>();
    public virtual ICollection<WfWorkItemLog> Logs { get; set; } = new List<WfWorkItemLog>();
}
