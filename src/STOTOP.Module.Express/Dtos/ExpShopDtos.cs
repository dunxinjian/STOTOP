using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 店铺详情
/// </summary>
public class ShopDto
{
    public string Name { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public bool IsShared { get; set; }
    public bool IsAutoCreated { get; set; }
    public bool NeedsAssignment { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public List<ShopAssignmentDto>? Assignments { get; set; }
}

/// <summary>
/// 店铺列表项
/// </summary>
public class ShopListItemDto
{
    public string Name { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public bool IsShared { get; set; }
    public bool IsAutoCreated { get; set; }
    public bool NeedsAssignment { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建店铺请求
/// </summary>
public class CreateShopRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public bool IsShared { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新店铺请求
/// </summary>
public class UpdateShopRequest
{
    public string? Platform { get; set; }
    public bool IsShared { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 店铺查询请求
/// </summary>
public class ShopQueryRequest : PagedRequest
{
    public int? Status { get; set; }
    public bool? NeedsAssignment { get; set; }
    public string? Platform { get; set; }
}
