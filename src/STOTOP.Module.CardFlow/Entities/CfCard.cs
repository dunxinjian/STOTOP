using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfCard : BaseEntity, IOrgScoped
{
    public long FFlowDefinitionId { get; set; }
    public long FFlowVersionId { get; set; }
    public string? FCardNumber { get; set; }
    public string? FTitle { get; set; }
    public string FStatus { get; set; } = "draft";
    public long FInitiatorId { get; set; }
    public string FInitiatorName { get; set; } = string.Empty;
    public DateTime FCreatedTime { get; set; }
    public DateTime? FSubmitTime { get; set; }
    public DateTime? FCompletedTime { get; set; }
    public long? FCurrentStageInstanceId { get; set; }
    public string? FDataJson { get; set; }
    public int FCurrentRound { get; set; }
    public long FOrgId { get; set; }
    public DateTime? FUpdatedTime { get; set; }
    /// <summary>批量触发时关联的批次ID（人工发起时为 null）</summary>
    public long? FBatchId { get; set; }
    /// <summary>关联的编排实例ID（null 表示独立卡片）</summary>
    public long? FOrchestrationInstanceId { get; set; }
    /// <summary>该卡片在编排 DAG 中对应的节点 ID</summary>
    public string? FOrchestrationNodeId { get; set; }
    /// <summary>来源模块：CRM / Expense / Loan 等外部发起上下文</summary>
    public string? FSourceModule { get; set; }
    /// <summary>来源业务类型：quotation / expenseReimburse 等</summary>
    public string? FSourceType { get; set; }
    /// <summary>来源业务主键</summary>
    public long? FSourceId { get; set; }
    /// <summary>处理完成后的返回地址</summary>
    public string? FReturnUrl { get; set; }
    /// <summary>外部入口带入的初始数据快照</summary>
    public string? FInitialDataJson { get; set; }
    /// <summary>来源业务展示标题</summary>
    public string? FSourceTitle { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
