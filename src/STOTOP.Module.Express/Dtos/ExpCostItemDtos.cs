namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 成本项目DTO
/// </summary>
public class CostItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsRebate { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 创建成本项目请求
/// </summary>
public class CreateCostItemRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsRebate { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 更新成本项目请求
/// </summary>
public class UpdateCostItemRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsRebate { get; set; }
    public int SortOrder { get; set; }
}
