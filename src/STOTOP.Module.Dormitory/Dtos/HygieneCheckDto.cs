namespace STOTOP.Module.Dormitory.Dtos;

/// <summary>
/// 卫生检查详情 DTO
/// </summary>
public class HygieneCheckDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public long InspectorId { get; set; }
    public string? InspectorName { get; set; }
    public DateTime CheckDate { get; set; }
    public int? Score { get; set; }
    public string? Result { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 卫生检查列表项 DTO
/// </summary>
public class HygieneCheckListItemDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public long InspectorId { get; set; }
    public string? InspectorName { get; set; }
    public DateTime CheckDate { get; set; }
    public int? Score { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建卫生检查请求
/// </summary>
public class CreateHygieneCheckRequest
{
    public long RoomId { get; set; }
    public long InspectorId { get; set; }
    public DateTime CheckDate { get; set; }
    public int? Score { get; set; }
    public string? Result { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新卫生检查请求
/// </summary>
public class UpdateHygieneCheckRequest
{
    public long InspectorId { get; set; }
    public DateTime CheckDate { get; set; }
    public int? Score { get; set; }
    public string? Result { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 卫生检查查询请求
/// </summary>
public class HygieneCheckQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? BuildingId { get; set; }
    public long? RoomId { get; set; }
    public long? InspectorId { get; set; }
    public DateTime? CheckDateStart { get; set; }
    public DateTime? CheckDateEnd { get; set; }
    public int? MinScore { get; set; }
    public int? MaxScore { get; set; }
}
