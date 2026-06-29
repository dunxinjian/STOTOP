namespace STOTOP.Module.Vehicle.Dtos;

/// <summary>
/// 租赁费用标准详情 DTO
/// </summary>
public class RentalStandardDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int ChargeCycle { get; set; }          // 1=月, 2=季, 3=年
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 租赁费用标准列表项 DTO
/// </summary>
public class RentalStandardListItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int ChargeCycle { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Status { get; set; }
}

/// <summary>
/// 创建租赁费用标准请求
/// </summary>
public class CreateRentalStandardRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int ChargeCycle { get; set; } = 1;
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新租赁费用标准请求
/// </summary>
public class UpdateRentalStandardRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int ChargeCycle { get; set; } = 1;
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 租赁费用标准查询请求
/// </summary>
public class RentalStandardQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? Status { get; set; }
}
