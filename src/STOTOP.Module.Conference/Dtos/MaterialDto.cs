namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 物品详情DTO
/// </summary>
public class MaterialDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Specification { get; set; }
    public int RequiredQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public string? Unit { get; set; }
    public string? AcquisitionMethod { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Supplier { get; set; }
    public string? SupplierContact { get; set; }
    public DateTime? RequiredDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Responsible { get; set; }
    public long? ScheduleId { get; set; }
    public string? ScheduleTitle { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 物品列表项DTO
/// </summary>
public class MaterialListItemDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int RequiredQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public string? Unit { get; set; }
    public string? AcquisitionMethod { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Responsible { get; set; }
    public DateTime? RequiredDate { get; set; }
}

/// <summary>
/// 创建物品请求
/// </summary>
public class CreateMaterialRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Specification { get; set; }
    public int RequiredQuantity { get; set; }
    public string? Unit { get; set; }
    public string? AcquisitionMethod { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Supplier { get; set; }
    public string? SupplierContact { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Responsible { get; set; }
    public long? ScheduleId { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新物品请求
/// </summary>
public class UpdateMaterialRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Specification { get; set; }
    public int RequiredQuantity { get; set; }
    public string? Unit { get; set; }
    public string? AcquisitionMethod { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Supplier { get; set; }
    public string? SupplierContact { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Status { get; set; }
    public string? Responsible { get; set; }
    public long? ScheduleId { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 物品查询请求
/// </summary>
public class MaterialQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public string? AcquisitionMethod { get; set; }
}

/// <summary>
/// 物品签收请求
/// </summary>
public class MaterialReceiveRequest
{
    /// <summary>签收数量</summary>
    public int ReceivedQuantity { get; set; }
    /// <summary>签收日期</summary>
    public DateTime ReceivedDate { get; set; }
}

/// <summary>
/// 物品归还请求
/// </summary>
public class MaterialReturnRequest
{
    /// <summary>归还日期</summary>
    public DateTime ReturnDate { get; set; }
    /// <summary>归还备注</summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 物品汇总统计DTO
/// </summary>
public class MaterialSummaryDto
{
    /// <summary>总物品数</summary>
    public int TotalCount { get; set; }
    /// <summary>已到位数</summary>
    public int ReceivedCount { get; set; }
    /// <summary>未到位数</summary>
    public int PendingCount { get; set; }
    /// <summary>总费用</summary>
    public decimal TotalCost { get; set; }
    /// <summary>按类别统计</summary>
    public List<MaterialCategorySummary> CategorySummaries { get; set; } = new();
}

/// <summary>
/// 物品类别统计子项
/// </summary>
public class MaterialCategorySummary
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalCost { get; set; }
    public int ReceivedCount { get; set; }
}
