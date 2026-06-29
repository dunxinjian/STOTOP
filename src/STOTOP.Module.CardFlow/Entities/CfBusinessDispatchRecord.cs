using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 业务派发记录：业务异常派发（OA / 工作任务 / 消息）记录（迁移自 CfBusinessDispatchRecord）
/// 注意：与 CfDispatchRecord（编排自动派发）语义不同，故独立命名。
/// </summary>
public class CfBusinessDispatchRecord : BaseEntity, IOrgScoped
{
    /// <summary>来源批次ID（CfBatch.FID）</summary>
    public long FBatchId { get; set; }
    /// <summary>来源批次号</summary>
    public string? FBatchNo { get; set; }
    /// <summary>关联错误记录ID（CfBatchError.FID）</summary>
    public long? FErrorId { get; set; }
    /// <summary>派发方式：OA/Task/Message</summary>
    public string FDispatchType { get; set; } = string.Empty;
    /// <summary>目标类型：WorkTask/NotifyMessage</summary>
    public string? FTargetType { get; set; }
    /// <summary>目标ID</summary>
    public long? FTargetId { get; set; }
    /// <summary>处理人</summary>
    public string? FAssignee { get; set; }
    /// <summary>处理人姓名</summary>
    public string? FAssigneeName { get; set; }
    /// <summary>状态：Pending/Processing/Completed/Overdue/Cancelled</summary>
    public string FStatus { get; set; } = string.Empty;
    /// <summary>异常类型</summary>
    public string? FExceptionType { get; set; }
    /// <summary>严重级别</summary>
    public string? FSeverityLevel { get; set; }
    /// <summary>派发说明</summary>
    public string? FDescription { get; set; }
    /// <summary>处理结果</summary>
    public string? FResult { get; set; }
    /// <summary>截止时间</summary>
    public DateTime? FDeadline { get; set; }
    /// <summary>完成时间</summary>
    public DateTime? FCompletedTime { get; set; }
    /// <summary>创建人</summary>
    public string? FCreator { get; set; }
    /// <summary>操作人</summary>
    public string? FOperator { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime? FUpdatedTime { get; set; }
    public long FOrgId { get; set; }
}
