namespace STOTOP.Module.Dormitory.Dtos;

/// <summary>
/// 设施详情 DTO
/// </summary>
public class FacilityDto
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 创建设施请求
/// </summary>
public class CreateFacilityRequest
{
    public string FacilityName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新设施请求
/// </summary>
public class UpdateFacilityRequest
{
    public string FacilityName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Remark { get; set; }
    public int Status { get; set; } = 1;
}
