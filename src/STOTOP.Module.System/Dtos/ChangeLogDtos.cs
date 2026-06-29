namespace STOTOP.Module.System.Dtos;

// 变更记录 DTO
public class ChangeLogDto
{
    public long Id { get; set; }
    public string BusinessType { get; set; } = string.Empty;
    public long BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string ChangeContent { get; set; } = string.Empty;
    public long? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public DateTime OperationTime { get; set; }
    public int DingTalkSyncStatus { get; set; }
    public DateTime? DingTalkSyncTime { get; set; }
    public string? DingTalkSyncResult { get; set; }
    public string? Remark { get; set; }
}

// 变更记录查询请求
public class ChangeLogQueryRequest
{
    public string? BusinessType { get; set; }
    public long? BusinessId { get; set; }
    public string? OperationType { get; set; }
    public long? OperatorId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
