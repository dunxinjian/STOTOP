using System.Text.Json.Serialization;

namespace STOTOP.Module.CardFlow.Dtos;

/// <summary>
/// 导入批次列表返回 DTO
/// </summary>
public class ImportBatchDto
{
    public long Id { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? FileTypeName { get; set; }
    public long FileSize { get; set; }
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailRows { get; set; }
    public int SkipRows { get; set; }

    /// <summary>前端兼容：successCount = SuccessRows</summary>
    [JsonPropertyName("successCount")]
    public int SuccessCount => SuccessRows;
    /// <summary>前端兼容：failedCount = FailRows</summary>
    [JsonPropertyName("failedCount")]
    public int FailedCount => FailRows;
    /// <summary>前端兼容：skippedCount = SkipRows</summary>
    [JsonPropertyName("skippedCount")]
    public int SkippedCount => SkipRows;
    /// <summary>前端兼容：errorCount = FailRows</summary>
    [JsonPropertyName("errorCount")]
    public int ErrorCount => FailRows;
    public string? SerialNumber { get; set; }
    public int Status { get; set; }
    public string? UploadMethod { get; set; }
    public string? DownloadTaskName { get; set; }
    public string? Operator { get; set; }
    public DateTime? ImportStartTime { get; set; }
    public DateTime? ImportEndTime { get; set; }
    public DateTime CreateTime { get; set; }
    public List<string>? ColumnNames { get; set; }  // Excel列名列表
    /// <summary>错误摘要</summary>
    public string? ErrorSummary { get; set; }
    /// <summary>文件组ID（多子批次共享同一个FileGroupId）</summary>
    public string? FileGroupId { get; set; }
    /// <summary>实际目标暂存表</summary>
    public string? ActualTargetTable { get; set; }
    /// <summary>父批次ID（子批次才有值，主批次为null）</summary>
    public long? ParentBatchId { get; set; }
    /// <summary>当前步骤名称（记录最后执行的步骤）</summary>
    public string? CurrentStepName { get; set; }
    /// <summary>当前执行阶段</summary>
    public string? CurrentPhase { get; set; }
    /// <summary>已处理行数</summary>
    public int? ProcessedRows { get; set; }
    /// <summary>进度百分比 0-100</summary>
    public int? ProgressPercent { get; set; }
    /// <summary>Agent处理总行数</summary>
    public int? ProgressTotalRows { get; set; }
    /// <summary>是否已卡住（前端按此决定是否显示重试/删除/指定管道等恢复性按钮）
    /// true 含义：处于处理中但超过10分钟未推进，或上传中但超过1小时未完成。</summary>
    public bool IsStale { get; set; }
    /// <summary>全局版本号（用于前端增量轮询水位线）</summary>
    public long Version { get; set; }
    /// <summary>子批次列表（前端分组展示用，API不直接填充）</summary>
    public List<ImportBatchDto>? Children { get; set; }
    /// <summary>插件执行快照（非终态批次及7天内终态批次有值，历史终态批次为 null）</summary>
    public List<PluginTrailItem>? Plugins { get; set; }
}

/// <summary>
/// 导入批次详情 DTO（含错误摘要）
/// </summary>
public class ImportBatchDetailDto : ImportBatchDto
{
    public string? FilePath { get; set; }
    public string? FileHash { get; set; }
    public new string? ErrorSummary { get; set; }
    public long? DownloadTaskId { get; set; }
}

/// <summary>
/// 导入错误明细 DTO
/// </summary>
public class ImportErrorDto
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public string? BatchNo { get; set; }
    public string? FileName { get; set; }
    public int RowNumber { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string? SeverityLevel { get; set; }
    public string? ErrorField { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuggestedFix { get; set; }
    public string? OriginalValue { get; set; }
    public string? QualityDimension { get; set; }
    /// <summary>派发状态: Pending/Dispatched/Processing/Completed/Ignored</summary>
    public string? DispatchStatus { get; set; }
    /// <summary>派发方式: OA/Task/Message</summary>
    public string? DispatchType { get; set; }
    /// <summary>关联的派发记录ID</summary>
    public long? DispatchRecordId { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 异常数据查询参数 DTO
/// </summary>
public class ImportErrorQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? BatchId { get; set; }
    public string? ErrorType { get; set; }
    public string? SeverityLevel { get; set; }
    public string? DispatchStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Keyword { get; set; }
}

/// <summary>
/// 批量忽略错误请求 DTO
/// </summary>
public class BatchIgnoreErrorsRequest
{
    public long[] ErrorIds { get; set; } = Array.Empty<long>();
}

/// <summary>
/// 重试请求 DTO
/// </summary>
public class RetryRequest
{
    /// <summary>重试模式：continue（从失败Agent处继续）| full-restart（全量清理从头开始）</summary>
    public string Mode { get; set; } = "continue";
}

/// <summary>
/// 派发记录 DTO
/// </summary>
public class BusinessDispatchRecordDto
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public string? BatchNo { get; set; }
    public long? ErrorId { get; set; }
    public string DispatchType { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    public string? Assignee { get; set; }
    public string? AssigneeName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? SeverityLevel { get; set; }
    public string? Description { get; set; }
    public string? Result { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? CompletedTime { get; set; }
    public string? Operator { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
}

/// <summary>
/// 创建派发请求 DTO
/// </summary>
public class CreateDispatchDto
{
    public long[] ErrorIds { get; set; } = Array.Empty<long>();
    public string DispatchType { get; set; } = string.Empty; // OA/Task/Message
    public string? Assignee { get; set; }
    public string? AssigneeName { get; set; }
    public string? Description { get; set; }
    public DateTime? Deadline { get; set; }
}

/// <summary>
/// 更新派发状态请求 DTO
/// </summary>
public class UpdateDispatchDto
{
    public string Status { get; set; } = string.Empty;
    public string? Result { get; set; }
}

/// <summary>
/// 导入总览统计 DTO
/// </summary>
public class ImportOverviewDto
{
    /// <summary>今日批次数量</summary>
    public int TodayBatchCount { get; set; }
    /// <summary>今日导入总行数</summary>
    public int TodayTotalRows { get; set; }
    /// <summary>成功率</summary>
    public double SuccessRate { get; set; }
    /// <summary>待处理异常数量</summary>
    public int PendingExceptionCount { get; set; }
    /// <summary>处理中任务数量</summary>
    public int ProcessingTaskCount { get; set; }
    /// <summary>每日趋势</summary>
    public List<DailyImportTrendDto> DailyTrend { get; set; } = new();
}

/// <summary>
/// 每日导入趋势 DTO
/// </summary>
public class DailyImportTrendDto
{
    public string Date { get; set; } = string.Empty;
    public int ImportCount { get; set; }
    public int ErrorCount { get; set; }
}

/// <summary>
/// 导入进度 DTO（SignalR 推送用）
/// </summary>
public class ImportProgressDto
{
    public string SessionId { get; set; } = string.Empty;
    public long BatchId { get; set; }
    public int CurrentRow { get; set; }
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public string CurrentStage { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
}

/// <summary>
/// 初始化分片上传请求
/// </summary>
public class ChunkUploadInitDto
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int TotalChunks { get; set; }
    public string? FileHash { get; set; }
}

/// <summary>
/// 初始化分片上传响应
/// </summary>
public class ChunkUploadInitResultDto
{
    public string UploadId { get; set; } = string.Empty;
    public List<int> ExistingChunks { get; set; } = new();
    /// <summary>是否为重复文件（数据库中已有相同文件指纹的批次）</summary>
    public bool IsDuplicate { get; set; }
    /// <summary>已存在批次的批次号</summary>
    public string? DuplicateBatchNo { get; set; }
    /// <summary>已存在批次的文件名</summary>
    public string? DuplicateFileName { get; set; }
}

/// <summary>
/// 完成分片上传请求
/// </summary>
public class CompleteChunkUploadDto
{
    public string UploadId { get; set; } = string.Empty;
}

/// <summary>
/// 完成上传响应
/// </summary>
public class ChunkUploadCompleteResultDto
{
    public long BatchId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public string? FileTypeName { get; set; }
    public int Status { get; set; }
    public List<string> ColumnNames { get; set; } = new();
}

/// <summary>
/// Agent简要信息
/// </summary>
public class AgentBriefDto
{
    public string Name { get; set; } = string.Empty;
    public string ImplementationType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>
/// 手动指定流程定义请求 DTO
/// </summary>
public class AssignPipelineDto
{
    /// <summary>流程定义ID</summary>
    public long? FlowDefinitionId { get; set; }
}

/// <summary>
/// 导入预览 DTO
/// </summary>
public class ImportPreviewDto
{
    public string? DetectedFileType { get; set; }
    public double Confidence { get; set; }
    public List<string> ColumnNames { get; set; } = new();
    public List<Dictionary<string, object?>> PreviewRows { get; set; } = new();
    public int TotalRows { get; set; }
    public bool IsDuplicate { get; set; }
    public string? DuplicateBatchNo { get; set; }
}

/// <summary>
/// 按日统计批次数量 DTO
/// </summary>
public class DailyBatchCountDto
{
    public string Date { get; set; } = string.Empty; // "yyyy-MM-dd"
    public int Count { get; set; }
}

/// <summary>
/// 批次列名信息 DTO
/// </summary>
public class BatchColumnsDto
{
    public List<string> ColumnNames { get; set; } = new();
    public string ColumnIdentifier { get; set; } = "";
    public int HeaderRowNumber { get; set; } = 1;
}

/// <summary>
/// 多流程触发结果 DTO（文件上传后为每个匹配流程各创建一个批次）
/// </summary>
public class BatchTriggerResultDto
{
    public long BatchId { get; set; }
    public long FlowDefinitionId { get; set; }
    public string FlowName { get; set; } = string.Empty;
}

/// <summary>
/// 批量撤销请求 DTO
/// </summary>
public class BatchRevokeRequest
{
    public List<long> BatchIds { get; set; } = new();
}

/// <summary>
/// 批量撤销结果 DTO
/// </summary>
public class BatchRevokeResult
{
    public List<long> Succeeded { get; set; } = new();
    public List<BatchRevokeSkipped> Skipped { get; set; } = new();
}

public class BatchRevokeSkipped
{
    public long Id { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 清空回收站结果 DTO
/// </summary>
public class ClearRecycleBinResult
{
    public int DeletedCount { get; set; }
    public int FailedCount { get; set; }
}

/// <summary>
/// 批次删除预检查结果 DTO
/// </summary>
public class BatchDeletePreCheck
{
    public bool CanDelete { get; set; }
    public bool HasAuditedVouchers { get; set; }
    public bool HasClosedPeriod { get; set; }
    public int AffectedVoucherCount { get; set; }
    public int AffectedRowCount { get; set; }
    public string? BlockReason { get; set; }
}

/// <summary>
/// 批次删除结果 DTO（包含级联删除凭证信息）
/// </summary>
public class BatchDeleteResult
{
    public bool Success { get; set; }
    /// <summary>删除的未审核凭证数</summary>
    public int DeletedVoucherCount { get; set; }
    /// <summary>保留的已审核凭证数</summary>
    public int RetainedVoucherCount { get; set; }
    /// <summary>保留的已审核凭证ID列表</summary>
    public List<long> RetainedVoucherIds { get; set; } = new();
    /// <summary>警告信息（如有已审核凭证被保留）</summary>
    public string? Warning { get; set; }
    /// <summary>需要用户确认（存在已审核凭证且期间未结账）</summary>
    public bool RequiresConfirmation { get; set; }
    /// <summary>已审核凭证数量</summary>
    public int AuditedVoucherCount { get; set; }
    /// <summary>包含已结账期间凭证</summary>
    public bool HasClosedPeriodVouchers { get; set; }
}

/// <summary>
/// 批次统计摘要（SignalR 推送用）
/// </summary>
public class BatchSummaryDto
{
    public int Success { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int Total { get; set; }
}

/// <summary>
/// 变更感知对账响应体
/// </summary>
public class BatchSyncResponse
{
    public List<BatchSyncItemDto> Batches { get; set; } = new();
    public long MaxVersion { get; set; }
}

/// <summary>
/// 变更感知对账 — 单个批次摘要
/// </summary>
public class BatchSyncItemDto
{
    public long BatchId { get; set; }
    public long Version { get; set; }
    /// <summary>CfBatch.FStatus (int): 0=解析中, 1=已暂存, ..., 6=失败, 7=部分完成, 8=已撤销</summary>
    public int Status { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? ImportType { get; set; }
    /// <summary>流程名称（从 CfFlowDefinition 关联获取），替代旧 PipelineName</summary>
    public string? FlowName { get; set; }
    /// <summary>向后兼容：FlowName 的别名</summary>
    [JsonPropertyName("pipelineName")]
    public string? PipelineName => FlowName;
    public DateTime CreateTime { get; set; }
    /// <summary>当前节点名称（直接从 CfBatch.FCurrentNodeName 获取），替代旧 CurrentAgentName</summary>
    public string? CurrentNodeName { get; set; }
    /// <summary>向后兼容：CurrentNodeName 的别名</summary>
    [JsonPropertyName("currentAgentName")]
    public string? CurrentAgentName => CurrentNodeName;
    public int? ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    /// <summary>向后兼容：ErrorMessage 的别名</summary>
    [JsonPropertyName("errorSummary")]
    public string? ErrorSummary => ErrorMessage;
    public bool IsRevoked { get; set; }

    /// <summary>总行数</summary>
    public int? TotalRows { get; set; }
    /// <summary>已处理行数（= 成功 + 失败 + 跳过）</summary>
    public int? ProcessedRows { get; set; }
    /// <summary>错误数 = FailedCount</summary>
    public int? ErrorCount { get; set; }
    /// <summary>成功行数</summary>
    public int? SuccessCount { get; set; }
    /// <summary>失败行数</summary>
    public int? FailedCount { get; set; }
    /// <summary>跳过行数</summary>
    public int? SkippedCount { get; set; }
    /// <summary>是否已卡住（处理中超10分钟未推进）</summary>
    public bool IsStale { get; set; }
    /// <summary>当前步骤名称</summary>
    public string? CurrentStepName { get; set; }
    /// <summary>插件执行快照（非终态批次及7天内终态批次有值）</summary>
    public List<PluginTrailItem>? Plugins { get; set; }
}

/// <summary>
/// 插件执行快照条目（用于 GetQueueBatches trail 快照内嵌）
/// </summary>
public class PluginTrailItem
{
    /// <summary>插件名称（如 ExcelInputAgent）</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>管道排序索引（0-based）</summary>
    public int Index { get; set; }
    /// <summary>执行状态：10=待处理,11=进行中,12=已完成,13=失败,14=已跳过</summary>
    public int Status { get; set; }
}
