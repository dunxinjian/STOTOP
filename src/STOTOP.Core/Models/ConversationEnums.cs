namespace STOTOP.Core.Models;

/// <summary>
/// 会话类型枚举
/// </summary>
public enum SessionType
{
    /// <summary>
    /// 发起流程
    /// </summary>
    ProcessInitiate = 0,

    /// <summary>
    /// 审批待办
    /// </summary>
    ApprovalTodo = 1,

    /// <summary>
    /// 系统通知
    /// </summary>
    SystemNotify = 2,

    /// <summary>
    /// 质量异常
    /// </summary>
    QualityAlert = 3,

    /// <summary>
    /// 数据中心
    /// </summary>
    DataCenter = 4,

    /// <summary>
    /// 任务协作
    /// </summary>
    TaskCollaboration = 5,

    /// <summary>
    /// 合同提醒
    /// </summary>
    ContractReminder = 6,

    /// <summary>
    /// 积分申请
    /// </summary>
    PointsApplication = 7,

    /// <summary>
    /// 系统告警
    /// </summary>
    SystemAlert = 8
}

/// <summary>
/// 卡片类型枚举
/// </summary>
public enum CardType
{
    /// <summary>
    /// 流程选择器
    /// </summary>
    ProcessSelector,

    /// <summary>
    /// 表单输入
    /// </summary>
    FormInput,

    /// <summary>
    /// 审批请求
    /// </summary>
    ApprovalRequest,

    /// <summary>
    /// 进度时间线
    /// </summary>
    ProgressTimeline,

    /// <summary>
    /// 结果展示
    /// </summary>
    Result,

    /// <summary>
    /// 信息展示
    /// </summary>
    Info,

    /// <summary>
    /// Pipeline进度
    /// </summary>
    PipelineProgress,

    /// <summary>
    /// 告警操作
    /// </summary>
    AlertAction,

    /// <summary>
    /// 任务分配
    /// </summary>
    TaskAssignment,

    /// <summary>
    /// 快速确认
    /// </summary>
    QuickConfirm,

    /// <summary>
    /// 数据摘要
    /// </summary>
    DataSummary,

    /// <summary>
    /// 提醒通知
    /// </summary>
    Reminder
}

/// <summary>
/// 会话状态枚举
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// 活跃
    /// </summary>
    Active = 0,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 1,

    /// <summary>
    /// 已关闭
    /// </summary>
    Closed = 2
}

/// <summary>
/// 卡片状态枚举
/// </summary>
public enum CardStatus
{
    /// <summary>
    /// 待交互
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 已交互
    /// </summary>
    Interacted = 1,

    /// <summary>
    /// 已过期
    /// </summary>
    Expired = 2
}

/// <summary>
/// 消息发送方枚举
/// </summary>
public enum MessageSender
{
    /// <summary>
    /// 系统
    /// </summary>
    System = 0,

    /// <summary>
    /// 用户
    /// </summary>
    User = 1
}

/// <summary>
/// 消息类型枚举
/// </summary>
public enum MessageType
{
    /// <summary>
    /// 文本
    /// </summary>
    Text,

    /// <summary>
    /// 卡片
    /// </summary>
    Card,

    /// <summary>
    /// 操作
    /// </summary>
    Action,

    /// <summary>
    /// 通知
    /// </summary>
    Notification
}
