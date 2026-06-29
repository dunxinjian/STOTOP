namespace STOTOP.Module.CRM.Dtos;

/// <summary>
/// 服务工单详情DTO（含处理记录）
/// </summary>
public class ServiceOrderDto
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public long? AssigneeId { get; set; }
    public int Category { get; set; }
    public int Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; }
    public DateTime? ResolvedTime { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? UpdaterName { get; set; }
    public DateTime? UpdatedTime { get; set; }
    public List<ServiceOrderLogDto> Logs { get; set; } = new();
}

/// <summary>
/// 服务工单列表项DTO
/// </summary>
public class ServiceOrderListItemDto
{
    public long Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public long? AssigneeId { get; set; }
    public int Category { get; set; }
    public int Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 服务工单处理记录DTO
/// </summary>
public class ServiceOrderLogDto
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long OperatorId { get; set; }
    public int OperationType { get; set; }
    public string? Content { get; set; }
    public string? Attachments { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建服务工单请求
/// </summary>
public class CreateServiceOrderRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public long? AssigneeId { get; set; }
    /// <summary>
    /// 工单类别：1=咨询, 2=投诉, 3=故障, 4=需求, 5=其他
    /// </summary>
    public int Category { get; set; }
    /// <summary>
    /// 优先级：1=紧急, 2=高, 3=中, 4=低
    /// </summary>
    public int Priority { get; set; } = 3;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// 更新服务工单请求
/// </summary>
public class UpdateServiceOrderRequest
{
    public long? AssigneeId { get; set; }
    public int Category { get; set; }
    public int Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// 工单操作请求（接单/处理/转派/关闭）
/// </summary>
public class ServiceOrderActionRequest
{
    public long OperatorId { get; set; }
    /// <summary>
    /// 操作类型：1=接单, 2=处理, 3=转派, 4=关闭
    /// </summary>
    public int OperationType { get; set; }
    public string? Content { get; set; }
    public string? Attachments { get; set; }
    /// <summary>
    /// 转派目标人员（仅转派时使用）
    /// </summary>
    public long? TransferToId { get; set; }
}

/// <summary>
/// 服务工单查询请求
/// </summary>
public class ServiceOrderQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? CustomerId { get; set; }
    public long? AssigneeId { get; set; }
    public int? Category { get; set; }
    public int? Priority { get; set; }
    public int? Status { get; set; }
}

/// <summary>
/// 工单统计DTO
/// </summary>
public class ServiceOrderStatisticsDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Processing { get; set; }
    public int WaitingConfirm { get; set; }
    public int Completed { get; set; }
    public int Closed { get; set; }
}
