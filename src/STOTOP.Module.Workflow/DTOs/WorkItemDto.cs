namespace STOTOP.Module.Workflow.DTOs;

public class WorkItemDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Type { get; set; }
    public int Source { get; set; }
    public int Status { get; set; }
    public int Priority { get; set; }
    public string? ChainId { get; set; }
    public int ChainSeq { get; set; }
    public string? DataScopeId { get; set; }
    public long CreatorId { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string? Module { get; set; }
    public string? BizType { get; set; }
    public long? BizId { get; set; }
    public string? DetailRoute { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? CompletedTime { get; set; }
    public string? Result { get; set; }
    public string? Remark { get; set; }

    // 计算字段
    public string StatusText => Status switch
    {
        0 => "待处理",
        1 => "处理中",
        2 => "已完成",
        3 => "已取消",
        4 => "已超时",
        _ => "未知"
    };

    public string TypeText => Type switch
    {
        1 => "任务",
        2 => "审批",
        3 => "预警",
        4 => "提醒",
        _ => "未知"
    };
}

public class WorkItemStatsDto
{
    public int PendingCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedTodayCount { get; set; }
    public int OverdueCount { get; set; }
}

public class CreateWorkItemRequest
{
    public long OrgId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Type { get; set; } = 1;
    public int Source { get; set; } = 1;
    public int Priority { get; set; } = 1;
    public string? ChainId { get; set; }
    public long? ParentWorkItemId { get; set; }
    public string? DataScopeId { get; set; }
    public long CreatorId { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string? Module { get; set; }
    public string? BizType { get; set; }
    public long? BizId { get; set; }
    public string? DetailRoute { get; set; }
    public DateTime? Deadline { get; set; }

    // 是否自动派发（为 true 时创建后自动调用 DispatchEngine）
    public bool AutoDispatch { get; set; } = true;
}
