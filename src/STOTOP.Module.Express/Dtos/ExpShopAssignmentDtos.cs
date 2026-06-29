using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 店铺归属详情
/// </summary>
public class ShopAssignmentDto
{
    public long Id { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public long ClientId { get; set; }
    public long PricePlanId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 店铺归属列表项
/// </summary>
public class ShopAssignmentListItemDto
{
    public long Id { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public long ClientId { get; set; }
    public long PricePlanId { get; set; }
    public string? PricePlanName { get; set; }
    public int PricePlanStatus { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建店铺归属请求
/// </summary>
public class CreateShopAssignmentRequest
{
    public string ShopName { get; set; } = string.Empty;
    public long ClientId { get; set; }
    public long? PricePlanId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remark { get; set; }
    /// <summary>新建报价方案（与 PricePlanId 二选一）</summary>
    public NewPricePlanInfo? NewPricePlan { get; set; }
}

/// <summary>
/// 更新店铺归属请求
/// </summary>
public class UpdateShopAssignmentRequest
{
    public string? ShopName { get; set; }
    public long? PricePlanId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 店铺归属查询请求
/// </summary>
public class ShopAssignmentQueryRequest : PagedRequest
{
    public long ClientId { get; set; }
    public string? ShopName { get; set; }
}

/// <summary>
/// 店铺归属批次
/// </summary>
public class ShopAssignmentBatchDto
{
    public long PricePlanId { get; set; }
    public string? PricePlanName { get; set; }
    public int PricePlanStatus { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int ShopCount { get; set; }
    public List<long> AssignmentIds { get; set; } = new();
}

/// <summary>
/// 批次中的店铺项
/// </summary>
public class BatchShopItemDto
{
    public long AssignmentId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 批量创建归属请求
/// </summary>
public class CreateBatchAssignmentRequest
{
    public long ClientId { get; set; }
    public long? PricePlanId { get; set; }
    public List<string>? ShopNames { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remark { get; set; }
    /// <summary>新建报价方案（与 PricePlanId 二选一）</summary>
    public NewPricePlanInfo? NewPricePlan { get; set; }
}

/// <summary>
/// 新建报价方案信息
/// </summary>
public class NewPricePlanInfo
{
    public string BrandCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int SettlementWeightStage { get; set; } = 1;
}
