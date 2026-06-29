namespace STOTOP.Module.Supplier.Dtos;

/// <summary>
/// 收款账户 DTO
/// </summary>
public class BankAccountDto
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public bool IsDefault { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 创建收款账户请求
/// </summary>
public class CreateBankAccountRequest
{
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public bool IsDefault { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新收款账户请求
/// </summary>
public class UpdateBankAccountRequest
{
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public bool IsDefault { get; set; }
    public string? Remark { get; set; }
}
