using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Dtos;

// ==================== 兑换商品 DTO ====================

/// <summary>
/// 兑换商品 - 列表 DTO
/// </summary>
public class RedeemItemListDto
{
    public long Id { get; set; }
    public string FUID { get; set; } = string.Empty;
    public long OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Category { get; set; }
    public string? Image { get; set; }
    public int RequiredPoints { get; set; }
    public int Stock { get; set; }
    public int RedeemedCount { get; set; }
    public int Status { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 兑换商品 - 详情 DTO
/// </summary>
public class RedeemItemDetailDto
{
    public long Id { get; set; }
    public string FUID { get; set; } = string.Empty;
    public long OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Image { get; set; }
    public int RequiredPoints { get; set; }
    public int Stock { get; set; }
    public int RedeemedCount { get; set; }
    public int Status { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 创建兑换商品请求
/// </summary>
public class CreateRedeemItemRequest
{
    public string Name { get; set; } = string.Empty;
    public int Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Image { get; set; }
    public int RequiredPoints { get; set; }
    public int Stock { get; set; } = -1;
    public int SortOrder { get; set; }
}

/// <summary>
/// 更新兑换商品请求
/// </summary>
public class UpdateRedeemItemRequest
{
    public string Name { get; set; } = string.Empty;
    public int Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Image { get; set; }
    public int RequiredPoints { get; set; }
    public int Stock { get; set; } = -1;
    public int SortOrder { get; set; }
    public int Status { get; set; }
}

/// <summary>
/// 兑换商品查询请求
/// </summary>
public class RedeemItemPagedRequest : PagedRequest
{
    public int? Category { get; set; }
    public int? Status { get; set; }
}

// ==================== 兑换记录 DTO ====================

/// <summary>
/// 兑换记录 - 列表 DTO
/// </summary>
public class RedeemRecordListDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public long ItemId { get; set; }
    public string? ItemName { get; set; }
    public int DeductedPoints { get; set; }
    public int Status { get; set; }
    public long? IssuerId { get; set; }
    public string? IssuerName { get; set; }
    public DateTime? IssueTime { get; set; }
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 执行兑换请求
/// </summary>
public class ExchangeRequest
{
    public long ItemId { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 确认发放请求
/// </summary>
public class DeliverRequest
{
    public string? Remark { get; set; }
}

/// <summary>
/// 兑换记录查询请求
/// </summary>
public class RedeemRecordPagedRequest : PagedRequest
{
    public long? UserId { get; set; }
    public long? ItemId { get; set; }
    public int? Status { get; set; }
}
