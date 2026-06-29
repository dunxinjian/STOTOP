using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 附加费详情（含作用域和配置项）
/// </summary>
public class PriceSurchargeDto
{
    public long Id { get; set; }
    public string SurchargeType { get; set; } = string.Empty;
    public int Scope { get; set; }  // 0=全局, 1=业务对象级, 2=报价级
    public string BrandCode { get; set; } = string.Empty;
    public string? NetworkPointCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? Name { get; set; }
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public List<SurchargeScopeDto> Scopes { get; set; } = new();
    public List<SurchargeItemDto> Items { get; set; } = new();
}

/// <summary>
/// 附加费列表项
/// </summary>
public class PriceSurchargeListItemDto
{
    public long Id { get; set; }
    public string SurchargeType { get; set; } = string.Empty;
    public int Scope { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string? NetworkPointCode { get; set; }
    public string? Name { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public List<SurchargeScopeDto> Scopes { get; set; } = new();
}

/// <summary>
/// 作用域关联DTO
/// </summary>
public class SurchargeScopeDto
{
    public string LinkedType { get; set; } = string.Empty;  // KH/DL/WD/YW/CB/YZ/QUOTATION
    public string LinkedId { get; set; } = string.Empty;
}

/// <summary>
/// 附加费配置项DTO
/// </summary>
public class SurchargeItemDto
{
    public long Id { get; set; }
    public int CalcMethod { get; set; }
    public int? WeightRoundingMethod { get; set; }
    public decimal? WeightFrom { get; set; }
    public decimal? WeightTo { get; set; }
    public int WeightType { get; set; }
    public int? DailyVolumeFrom { get; set; }
    public int? DailyVolumeTo { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
    public List<SurchargeDestDto> Destinations { get; set; } = new();
}

/// <summary>
/// 附加费目的地DTO
/// </summary>
public class SurchargeDestDto
{
    public long Id { get; set; }
    public int DestType { get; set; }
    public int? ProvinceId { get; set; }
    public string? CityName { get; set; }
}

/// <summary>
/// 创建附加费请求
/// </summary>
public class CreatePriceSurchargeRequest
{
    public string SurchargeType { get; set; } = string.Empty;
    public int Scope { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string? NetworkPointCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? Name { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Remark { get; set; }
    public List<SurchargeScopeDto> Scopes { get; set; } = new();
    public List<SurchargeItemInput> Items { get; set; } = new();
}

/// <summary>
/// 更新附加费请求。Name/IsActive/Remark 为 null 表示"不修改"——
/// 前端编辑页不回传这三个字段，非空默认值会把停用状态静默重置为启用、把名称清空。
/// </summary>
public class UpdatePriceSurchargeRequest
{
    public string SurchargeType { get; set; } = string.Empty;
    public int Scope { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string? NetworkPointCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? Name { get; set; }
    public bool? IsActive { get; set; }
    public string? Remark { get; set; }
    public List<SurchargeScopeDto> Scopes { get; set; } = new();
    public List<SurchargeItemInput> Items { get; set; } = new();
}

/// <summary>
/// 附加费配置项输入
/// </summary>
public class SurchargeItemInput
{
    public int CalcMethod { get; set; }
    public int? WeightRoundingMethod { get; set; }
    public decimal? WeightFrom { get; set; }
    public decimal? WeightTo { get; set; }
    public int WeightType { get; set; } = 1;
    public int? DailyVolumeFrom { get; set; }
    public int? DailyVolumeTo { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; } = 0;
    public List<SurchargeDestInput> Destinations { get; set; } = new();
}

/// <summary>
/// 附加费目的地输入
/// </summary>
public class SurchargeDestInput
{
    public int DestType { get; set; } = 1;
    public int? ProvinceId { get; set; }
    public string? CityName { get; set; }
}

/// <summary>
/// 附加费查询请求
/// </summary>
public class PriceSurchargeQueryRequest : PagedRequest
{
    public string? BrandCode { get; set; }
    public int? Scope { get; set; }
    public string? NetworkPointCode { get; set; }
    public string? SurchargeType { get; set; }
    public bool? IsActive { get; set; }
    public new string? Keyword { get; set; }
}
