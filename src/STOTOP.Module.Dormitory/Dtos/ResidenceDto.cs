namespace STOTOP.Module.Dormitory.Dtos;

/// <summary>
/// 入住记录详情 DTO
/// </summary>
public class ResidenceDto
{
    public long Id { get; set; }
    public long BedId { get; set; }
    public string BedNumber { get; set; } = string.Empty;
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public long BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 入住记录列表项 DTO
/// </summary>
public class ResidenceListItemDto
{
    public long Id { get; set; }
    public long BedId { get; set; }
    public string BedNumber { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建入住记录请求
/// </summary>
public class CreateResidenceRequest
{
    public long BedId { get; set; }
    public long EmployeeId { get; set; }
    public DateTime CheckInDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新入住记录请求
/// </summary>
public class UpdateResidenceRequest
{
    public string? Remark { get; set; }
}

/// <summary>
/// 退宿请求
/// </summary>
public class CheckOutRequest
{
    public DateTime CheckOutDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 入住记录查询请求
/// </summary>
public class ResidenceQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public long? BuildingId { get; set; }
    public long? RoomId { get; set; }
    public long? BedId { get; set; }
    public long? EmployeeId { get; set; }
    public int? Status { get; set; }
}
