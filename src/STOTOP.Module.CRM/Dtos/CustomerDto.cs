namespace STOTOP.Module.CRM.Dtos;

/// <summary>
/// 客户详情DTO（含联系人）
/// </summary>
public class CustomerDto
{
    public string Id { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? ServiceObjectId { get; set; }
    /// <summary>简称</summary>
    public string ShortName { get; set; } = string.Empty;
    /// <summary>全称</summary>
    public string? FullName { get; set; }
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Industry { get; set; }
    public string? Scale { get; set; }
    public int Status { get; set; }
    public long? OrgId { get; set; }
    public long? BdEmployeeId { get; set; }
    public long? MaintenanceEmployeeId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? UpdaterName { get; set; }
    public DateTime? UpdatedTime { get; set; }
    public List<CustomerContactDto> Contacts { get; set; } = new();

    // ===== 客户扩展属性（EXP业务对象 F类型=1 迁入） =====
    public string? SenderAddress { get; set; }
    public string? OfficeAddress { get; set; }
    public string? CargoType { get; set; }
    public decimal? PrepayPerTicket { get; set; }
    public string? AttachmentPath { get; set; }
    public string? SourceClientType { get; set; }
    public string? SettlementModeText { get; set; }
    public string? WarehouseCategory { get; set; }
    public string? CutoffTime { get; set; }
    public string? RequiredArea { get; set; }
    public string? DailyOrderVolume { get; set; }
    public string? SkuStructure { get; set; }
    public string? CombinedProducts { get; set; }
    public string? Platform { get; set; }
    public string? ExpressPriority { get; set; }
    public string? RemoteDelivery { get; set; }
    public string? ReturnRestock { get; set; }
    public string? CustomerSoftware { get; set; }
    public string? TempClientId { get; set; }
}

/// <summary>
/// 客户列表项DTO
/// </summary>
public class CustomerListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Industry { get; set; }
    public int Status { get; set; }
    public long? OrgId { get; set; }
    public long? BdEmployeeId { get; set; }
    public string? ServiceObjectId { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 客户联系人DTO
/// </summary>
public class CustomerContactDto
{
    public long Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Position { get; set; }
    public string? RoleTag { get; set; }
    public bool IsPrimary { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建客户请求
/// </summary>
public class CreateCustomerRequest
{
    public string? Code { get; set; }
    /// <summary>简称（必填）</summary>
    public string ShortName { get; set; } = string.Empty;
    /// <summary>全称（可选）</summary>
    public string? FullName { get; set; }
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Industry { get; set; }
    public string? Scale { get; set; }
    public long? OrgId { get; set; }
    public long? BdEmployeeId { get; set; }
    public long? MaintenanceEmployeeId { get; set; }
    public List<CreateContactRequest>? Contacts { get; set; }

    // ===== 客户扩展属性 =====
    public string? SenderAddress { get; set; }
    public string? OfficeAddress { get; set; }
    public string? CargoType { get; set; }
    public decimal? PrepayPerTicket { get; set; }
    public string? AttachmentPath { get; set; }
    public string? SourceClientType { get; set; }
    public string? SettlementModeText { get; set; }
    public string? WarehouseCategory { get; set; }
    public string? CutoffTime { get; set; }
    public string? RequiredArea { get; set; }
    public string? DailyOrderVolume { get; set; }
    public string? SkuStructure { get; set; }
    public string? CombinedProducts { get; set; }
    public string? Platform { get; set; }
    public string? ExpressPriority { get; set; }
    public string? RemoteDelivery { get; set; }
    public string? ReturnRestock { get; set; }
    public string? CustomerSoftware { get; set; }
    public string? TempClientId { get; set; }
}

/// <summary>
/// 更新客户请求
/// </summary>
public class UpdateCustomerRequest
{
    public string? Code { get; set; }
    /// <summary>简称（必填）</summary>
    public string ShortName { get; set; } = string.Empty;
    /// <summary>全称（可选）</summary>
    public string? FullName { get; set; }
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Industry { get; set; }
    public string? Scale { get; set; }
    public long? OrgId { get; set; }
    public long? BdEmployeeId { get; set; }
    public long? MaintenanceEmployeeId { get; set; }
    public List<CreateContactRequest>? Contacts { get; set; }

    // ===== 客户扩展属性 =====
    public string? SenderAddress { get; set; }
    public string? OfficeAddress { get; set; }
    public string? CargoType { get; set; }
    public decimal? PrepayPerTicket { get; set; }
    public string? AttachmentPath { get; set; }
    public string? SourceClientType { get; set; }
    public string? SettlementModeText { get; set; }
    public string? WarehouseCategory { get; set; }
    public string? CutoffTime { get; set; }
    public string? RequiredArea { get; set; }
    public string? DailyOrderVolume { get; set; }
    public string? SkuStructure { get; set; }
    public string? CombinedProducts { get; set; }
    public string? Platform { get; set; }
    public string? ExpressPriority { get; set; }
    public string? RemoteDelivery { get; set; }
    public string? ReturnRestock { get; set; }
    public string? CustomerSoftware { get; set; }
    public string? TempClientId { get; set; }
}

/// <summary>
/// 创建联系人请求
/// </summary>
public class CreateContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Position { get; set; }
    public string? RoleTag { get; set; }
    public bool IsPrimary { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 客户查询请求
/// </summary>
public class CustomerQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? Status { get; set; }
    public long? OrgId { get; set; }
    public long? BdEmployeeId { get; set; }
    public string? Industry { get; set; }
}

/// <summary>
/// 客户流转请求
/// </summary>
public class TransferCustomerRequest
{
    public int TransferType { get; set; }
    public long? NewOrgId { get; set; }
    public long? NewBdEmployeeId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// 客户去重检测请求
/// </summary>
public class CustomerDuplicateCheckRequest
{
    public string? ShortName { get; set; }
    public string? Phone { get; set; }
}

/// <summary>
/// 客户时间线项DTO
/// </summary>
public class CustomerTimelineItemDto
{
    public string Type { get; set; } = string.Empty;
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public DateOnly OccurredTime { get; set; }
    public string? CreatorName { get; set; }
}

/// <summary>
/// 更新客户状态请求
/// </summary>
public class UpdateCustomerStatusRequest
{
    public int Status { get; set; }
}

/// <summary>客户状态统计</summary>
public class CustomerStatisticsDto
{
    public int TotalCount { get; set; }
    public List<CustomerStatusGroupDto> ByStatus { get; set; } = new();
}

/// <summary>按状态分组统计</summary>
public class CustomerStatusGroupDto
{
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
}
