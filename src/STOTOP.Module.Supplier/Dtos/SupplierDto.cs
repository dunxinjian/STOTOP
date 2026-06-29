namespace STOTOP.Module.Supplier.Dtos;

/// <summary>
/// 供应商详情 DTO（包含收款账户）
/// </summary>
public class SupplierDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? CreditCode { get; set; }
    public string? TaxNumber { get; set; }
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public List<BankAccountDto> BankAccounts { get; set; } = new();
}

/// <summary>
/// 供应商列表项 DTO（不含收款账户）
/// </summary>
public class SupplierListItemDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建供应商请求
/// </summary>
public class CreateSupplierRequest
{
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? CreditCode { get; set; }
    public string? TaxNumber { get; set; }
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Remark { get; set; }
    public List<CreateBankAccountRequest>? BankAccounts { get; set; }
}

/// <summary>
/// 更新供应商请求
/// </summary>
public class UpdateSupplierRequest
{
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? CreditCode { get; set; }
    public string? TaxNumber { get; set; }
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Remark { get; set; }
    public List<CreateBankAccountRequest>? BankAccounts { get; set; }
}

/// <summary>
/// 更新供应商状态请求
/// </summary>
public class UpdateSupplierStatusRequest
{
    public int Status { get; set; }
}

/// <summary>
/// 供应商查询请求
/// </summary>
public class SupplierQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? Status { get; set; }
}
