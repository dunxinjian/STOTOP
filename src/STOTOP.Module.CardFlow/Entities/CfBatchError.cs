using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 批次错误记录（迁移自 DcImportError）
/// </summary>
public class CfBatchError : BaseEntity, IOrgScoped
{
    /// <summary>关联批次ID（CfBatch.FID）</summary>
    public long FBatchId { get; set; }
    /// <summary>暂存ID</summary>
    public long? FStagingId { get; set; }
    /// <summary>行号</summary>
    public int FRowNumber { get; set; }
    /// <summary>错误类型</summary>
    public string FErrorType { get; set; } = string.Empty;
    /// <summary>严重级别</summary>
    public string? FSeverityLevel { get; set; }
    /// <summary>错误字段</summary>
    public string? FErrorField { get; set; }
    /// <summary>错误信息</summary>
    public string? FErrorMessage { get; set; }
    /// <summary>建议修复</summary>
    public string? FSuggestedFix { get; set; }
    /// <summary>原始值</summary>
    public string? FOriginalValue { get; set; }
    /// <summary>质量维度</summary>
    public string? FQualityDimension { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;

    // 派发相关字段
    /// <summary>派发状态：Pending/Dispatched/Processing/Completed/Ignored</summary>
    public string? FDispatchStatus { get; set; }
    /// <summary>派发方式：OA/Task/Message</summary>
    public string? FDispatchType { get; set; }
    /// <summary>关联派发记录ID</summary>
    public long? FDispatchRecordId { get; set; }

    // AutoPlugin → WorkItem 对接
    /// <summary>关联工作项ID</summary>
    public long? FWorkItemId { get; set; }
    /// <summary>问题类型编码</summary>
    public string FIssueType { get; set; } = string.Empty;
    /// <summary>处理结果：0=未处理 1=已修正重跑 2=已跳过</summary>
    public int FProcessResult { get; set; }

    // 统一异常处理闭环
    /// <summary>处理状态：Pending/Processing/Resolved/Ignored/Failed</summary>
    public string FResolutionStatus { get; set; } = "Pending";
    /// <summary>处理结果上下文 JSON</summary>
    public string? FResolutionPayloadJson { get; set; }
    /// <summary>处理人ID</summary>
    public long? FResolvedBy { get; set; }
    /// <summary>处理时间</summary>
    public DateTime? FResolvedTime { get; set; }
    /// <summary>重跑状态：None/Requested/Running/Succeeded/Failed</summary>
    public string? FRetryStatus { get; set; }
    /// <summary>重跑消息</summary>
    public string? FRetryMessage { get; set; }

    public long FOrgId { get; set; }
}
