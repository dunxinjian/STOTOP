namespace STOTOP.Module.Dormitory.Dtos;

/// <summary>
/// 访客登记详情 DTO
/// </summary>
public class VisitorDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string VisitorName { get; set; } = string.Empty;
    public string? VisitorPhone { get; set; }
    public string? VisitorIdCard { get; set; }
    public string? VisitReason { get; set; }
    public long? VisitedPersonId { get; set; }
    public string? VisitedPersonName { get; set; }
    public DateTime ArrivalTime { get; set; }
    public DateTime? DepartureTime { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 访客登记列表项 DTO
/// </summary>
public class VisitorListItemDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string VisitorName { get; set; } = string.Empty;
    public string? VisitorPhone { get; set; }
    public DateTime ArrivalTime { get; set; }
    public DateTime? DepartureTime { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建访客登记请求
/// </summary>
public class CreateVisitorRequest
{
    public long RoomId { get; set; }
    public string VisitorName { get; set; } = string.Empty;
    public string? VisitorPhone { get; set; }
    public string? VisitorIdCard { get; set; }
    public string? VisitReason { get; set; }
    public long? VisitedPersonId { get; set; }
    public DateTime ArrivalTime { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新访客登记请求
/// </summary>
public class UpdateVisitorRequest
{
    public string VisitorName { get; set; } = string.Empty;
    public string? VisitorPhone { get; set; }
    public string? VisitorIdCard { get; set; }
    public string? VisitReason { get; set; }
    public long? VisitedPersonId { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 访客登记查询请求
/// </summary>
public class VisitorQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? BuildingId { get; set; }
    public long? RoomId { get; set; }
    public string? Keyword { get; set; }
    public DateTime? ArrivalDateStart { get; set; }
    public DateTime? ArrivalDateEnd { get; set; }
}
