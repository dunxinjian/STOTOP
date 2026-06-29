using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 异常单状态
/// </summary>
public enum ExceptionStatus
{
    /// <summary>待处理</summary>
    Pending = 0,
    /// <summary>处理中</summary>
    Processing = 1,
    /// <summary>已超时</summary>
    Overdue = 2,
    /// <summary>已关闭</summary>
    Closed = 3
}

/// <summary>
/// 异常类型
/// </summary>
public enum ExceptionType
{
    /// <summary>数据异常</summary>
    DataAnomaly = 0,
    /// <summary>流程超时</summary>
    ProcessTimeout = 1,
    /// <summary>规则违规</summary>
    RuleViolation = 2
}

/// <summary>
/// 优先级
/// </summary>
public enum ExceptionPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// 派发方式
/// </summary>
public enum DispatchMethod
{
    /// <summary>OA流程</summary>
    OAProcess = 0,
    /// <summary>工作任务</summary>
    WorkTask = 1,
    /// <summary>消息预警</summary>
    MessageAlert = 2
}

/// <summary>
/// 异常单
/// </summary>
public class QlException : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string FExceptionNo { get; set; } = string.Empty;
    public string FTitle { get; set; } = string.Empty;
    public string FDescription { get; set; } = string.Empty;
    public int FType { get; set; }
    public int FStatus { get; set; }
    public int FPriority { get; set; }
    public long? FRuleId { get; set; }
    public string? FSource { get; set; }
    public string? FRelatedModule { get; set; }
    public long? FRelatedEntityId { get; set; }
    public long? FAssigneeId { get; set; }
    public int? FDispatchMethod { get; set; }
    public long? FDispatchEntityId { get; set; }
    public DateTime? FDeadline { get; set; }
    public DateTime? FClosedTime { get; set; }
    public long FCreatorId { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
