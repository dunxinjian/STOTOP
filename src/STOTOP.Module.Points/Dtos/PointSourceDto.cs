namespace STOTOP.Module.Points.Dtos;

/// <summary>
/// 积分来源 - 列表/详情 DTO
/// </summary>
public class PointSourceDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 创建积分来源请求
/// </summary>
public class CreatePointSourceRequest
{
    public string SourceName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 更新积分来源请求
/// </summary>
public class UpdatePointSourceRequest
{
    public string SourceName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
}
