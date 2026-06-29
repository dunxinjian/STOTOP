using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 运单详情
/// </summary>
public class WaybillDto
{
    public long Id { get; set; }
    public string WaybillNo { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? SenderProvince { get; set; }
    public int? ReceiverProvinceId { get; set; }
    public string? ProvinceName { get; set; }
    public decimal? PickupWeight { get; set; }
    public decimal? TransitWeight { get; set; }
    public decimal? DeliveryWeight { get; set; }
    public decimal? BagWeight { get; set; }
    public decimal? BubbleWeight { get; set; }
    public decimal? HqWeight { get; set; }
    public bool? IsDirectDelivery { get; set; }
    public decimal? ActualWeight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? VolumetricWeight { get; set; }
    public decimal? BillableWeight { get; set; }
    public decimal? DeclaredValue { get; set; }
    public DateTime WaybillDate { get; set; }
    public long? ImportBatchId { get; set; }
    public string? ClientAlias { get; set; }
    public int BillingStatus { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 运单列表项
/// </summary>
public class WaybillListItemDto
{
    public long Id { get; set; }
    public string WaybillNo { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? ProvinceName { get; set; }
    public decimal? PickupWeight { get; set; }
    public decimal? ActualWeight { get; set; }
    public decimal? BillableWeight { get; set; }
    public DateTime WaybillDate { get; set; }
    public int BillingStatus { get; set; }
}

/// <summary>
/// 运单查询请求
/// </summary>
public class WaybillQueryRequest : PagedRequest
{
    public string? BrandCode { get; set; }
    public string? ShopName { get; set; }
    public string? ClientId { get; set; }
    public int? ReceiverProvinceId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int? BillingStatus { get; set; }
    public string? WaybillNo { get; set; }
}

/// <summary>
/// 运单导入结果
/// </summary>
public class WaybillImportResult
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public int SkipCount { get; set; }
    public int NewShopsFound { get; set; }
    public List<WaybillImportError> Errors { get; set; } = new();
}

/// <summary>
/// 运单导入错误
/// </summary>
public class WaybillImportError
{
    public int RowNumber { get; set; }
    public string? WaybillNo { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// 店铺发现结果
/// </summary>
public class ShopDiscoveryResult
{
    public int NewShopsCount { get; set; }
    public List<string> ShopNames { get; set; } = new();
}
