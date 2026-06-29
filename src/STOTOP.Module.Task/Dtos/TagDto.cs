namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 标签列表DTO
/// </summary>
public class TagListDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public long OrgId { get; set; }
    public int Sort { get; set; }
    public int TaskCount { get; set; }
}

/// <summary>
/// 创建标签请求（含名称+颜色）
/// </summary>
public class CreateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int Sort { get; set; } = 0;
}

/// <summary>
/// 更新标签请求（含名称+颜色）
/// </summary>
public class UpdateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int Sort { get; set; } = 0;
}
