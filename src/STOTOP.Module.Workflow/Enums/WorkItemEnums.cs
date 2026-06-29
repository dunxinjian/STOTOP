namespace STOTOP.Module.Workflow.Enums;

/// <summary>工作项类型</summary>
public enum WorkItemType
{
    Task = 1,        // 普通任务
    Approval = 2,    // 审批
    Alert = 3,       // 预警
    Reminder = 4     // 提醒
}

/// <summary>工作项状态</summary>
public enum WorkItemStatus
{
    Pending = 0,     // 待处理
    InProgress = 1,  // 处理中
    Completed = 2,   // 已完成
    Cancelled = 3,   // 已取消
    Expired = 4,     // 已超时
    Dismissed = 5    // 已关闭（WorkHub手动关闭）
}

/// <summary>工作项优先级</summary>
public enum WorkItemPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>工作项来源</summary>
public enum WorkItemSource
{
    Pipeline = 1,        // 数据管道产生
    Manual = 2,          // 手动创建
    Schedule = 3,        // 定时任务产生
    Escalation = 4,      // 升级产生
    Approval = 5         // 审批流程产生
}

/// <summary>派发模式</summary>
public enum DispatchMode
{
    Auto = 1,        // 全自动（规则匹配→直接分配）
    Hybrid = 2,      // 半自动（规则推荐→人工确认）
    Manual = 3       // 全手动（进入待分配池）
}

/// <summary>链路事件类型</summary>
public enum ChainEventType
{
    Created = 1,         // 链路创建
    StageStarted = 2,   // 阶段开始
    StageCompleted = 3,  // 阶段完成
    IssueFound = 4,     // 发现问题
    Assigned = 5,        // 已分配
    Resolved = 6,        // 已解决
    Commented = 7,       // 有评论
    Escalated = 8,       // 已升级
    Revoked = 9          // 已撤销
}
