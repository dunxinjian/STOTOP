using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF 批次：批量触发（文件上传/定时/条件）创建的卡片集合
/// 状态机：0=解析中, 1=已暂存, 2=质检中, 3=已创建卡片, 4=处理中, 5=已完成
/// </summary>
public class CfBatch : BaseEntity, IOrgScoped
{
    public long FFlowDefinitionId { get; set; }
    public long FOrgId { get; set; }
    public long FTriggeredById { get; set; }
    public DateTime FTriggeredTime { get; set; }
    /// <summary>触发类型：human / fileUpload / scheduled / conditional</summary>
    public string FTriggerType { get; set; } = string.Empty;
    public string? FFilePath { get; set; }
    /// <summary>原始文件名</summary>
    public string? FFileName { get; set; }
    /// <summary>跳过行数（默认0）</summary>
    public int FSkipRows { get; set; }
    /// <summary>Excel 列 → Schema 字段映射 JSON</summary>
    public string? FColumnMappingJson { get; set; }
    public int FTotalRows { get; set; }
    public int FSuccessRows { get; set; }
    public int FFailedRows { get; set; }
    /// <summary>0=解析中, 1=已暂存, 2=质检中, 3=已创建卡片, 4=处理中, 5=已完成</summary>
    public int FStatus { get; set; }
    /// <summary>
    /// 当前批次级节点排序号（记录批次在批次级节点链中的进度位置）
    /// </summary>
    public int? FCurrentBatchStageOrder { get; set; }
    public bool FIsRevoked { get; set; }
    public DateTime? FRevokedTime { get; set; }
    public long? FRevokedById { get; set; }
    public string? FErrorMessage { get; set; }
    /// <summary>关联的编排实例ID（null 表示独立批次）</summary>
    public long? FOrchestrationInstanceId { get; set; }
    /// <summary>该批次在编排 DAG 中对应的节点 ID</summary>
    public string? FOrchestrationNodeId { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime? FUpdatedTime { get; set; }
    /// <summary>批次号（唯一标识，O2：初始 NULLABLE，Seeder 回填后加约束）</summary>
    public string? FBatchNo { get; set; }
    /// <summary>文件 Hash</summary>
    public string? FFileHash { get; set; }
    /// <summary>文件大小（字节）</summary>
    public long? FFileSize { get; set; }
    /// <summary>导入执行开始时间</summary>
    public DateTime? FImportStartTime { get; set; }
    /// <summary>导入执行结束时间</summary>
    public DateTime? FImportEndTime { get; set; }
    /// <summary>当前节点名称</summary>
    public string? FCurrentNodeName { get; set; }
    /// <summary>变更感知版本号（全局单调递增）</summary>
    public long FChangeVersion { get; set; }
    /// <summary>进度百分比 0-100</summary>
    public int? FProgressPercent { get; set; }
    /// <summary>账套ID</summary>
    public long? FAccountSetId { get; set; }
    /// <summary>上传方式："manual"/"auto"</summary>
    public string? FUploadMethod { get; set; }
    /// <summary>关联工作项ID</summary>
    public long? FWorkItemId { get; set; }
    /// <summary>经路由规则确定的实际写入暂存表名</summary>
    public string? FActualTargetTable { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
