namespace STOTOP.Module.Dormitory.Dtos;

/// <summary>
/// 床位 DTO
/// </summary>
public class BedDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string BedNumber { get; set; } = string.Empty;
    public string BedType { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 床位列表项 DTO
/// </summary>
public class BedListItemDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string BedNumber { get; set; } = string.Empty;
    public string BedType { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建床位请求
/// </summary>
public class CreateBedRequest
{
    public string BedNumber { get; set; } = string.Empty;
    public string BedType { get; set; } = "lower";
    public string? Remark { get; set; }
}

/// <summary>
/// 更新床位请求
/// </summary>
public class UpdateBedRequest
{
    public string BedNumber { get; set; } = string.Empty;
    public string BedType { get; set; } = "lower";
    public string? Remark { get; set; }
    /// <summary>床位状态（0/1空闲、2已入住、3维修中），不传则保持原值</summary>
    public int? Status { get; set; }
}

/// <summary>
/// 更新床位状态请求
/// </summary>
public class UpdateBedStatusRequest
{
    public int Status { get; set; }
}

/// <summary>
/// 床位查询请求
/// </summary>
public class BedQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? Status { get; set; }
}
