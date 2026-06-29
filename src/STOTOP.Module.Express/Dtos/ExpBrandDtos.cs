using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 品牌详情
/// </summary>
public class BrandDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 品牌列表项
/// </summary>
public class BrandListItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建品牌请求
/// </summary>
public class CreateBrandRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新品牌请求
/// </summary>
public class UpdateBrandRequest
{
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 品牌查询请求
/// </summary>
public class BrandQueryRequest : PagedRequest
{
    public int? Status { get; set; }
}
