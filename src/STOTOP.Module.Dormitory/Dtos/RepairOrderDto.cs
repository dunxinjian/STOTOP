namespace STOTOP.Module.Dormitory.Dtos;

/// <summary>
/// 报修工单详情 DTO
/// </summary>
public class RepairOrderDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public long ReporterId { get; set; }
    public string? ReporterName { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public long? HandlerId { get; set; }
    public string? HandlerName { get; set; }
    public string? Result { get; set; }
    public DateTime? HandledTime { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 报修工单列表项 DTO
/// </summary>
public class RepairOrderListItemDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public long ReporterId { get; set; }
    public string? ReporterName { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建报修工单请求
/// </summary>
public class CreateRepairOrderRequest
{
    public long RoomId { get; set; }
    public long ReporterId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
}

/// <summary>
/// 更新报修工单请求
/// </summary>
public class UpdateRepairOrderRequest
{
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
}

/// <summary>
/// 处理报修工单请求
/// </summary>
public class HandleRepairOrderRequest
{
    public long HandlerId { get; set; }
    public string Result { get; set; } = string.Empty;
    public int Status { get; set; } = 3;
}

/// <summary>
/// 报修工单查询请求
/// </summary>
public class RepairOrderQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? BuildingId { get; set; }
    public long? RoomId { get; set; }
    public long? ReporterId { get; set; }
    public int? Status { get; set; }
    public int? Priority { get; set; }
}
