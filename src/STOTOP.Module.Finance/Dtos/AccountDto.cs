namespace STOTOP.Module.Finance.Dtos;

public class AccountDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BalanceDirection { get; set; } = string.Empty;
    public int Level { get; set; }
    public long ParentId { get; set; }
    public bool IsLeaf { get; set; }
    public string? Auxiliary { get; set; }
    public string? Currency { get; set; }
    public string? Unit { get; set; }
    public bool EnableStatus { get; set; }
}

public class AccountTreeDto : AccountDto
{
    public List<AccountTreeDto> Children { get; set; } = new();
}

/// <summary>
/// 科目选择器轻量 DTO
/// </summary>
public class AccountSelectorDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long ParentId { get; set; }
    public bool IsLeaf { get; set; }
}

public class CreateAccountRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BalanceDirection { get; set; } = string.Empty;
    public long ParentId { get; set; }
    public string? Auxiliary { get; set; }
    public string? Currency { get; set; }
    public string? Unit { get; set; }
}

public class UpdateAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Auxiliary { get; set; }
    public string? Currency { get; set; }
    public string? Unit { get; set; }
}

public class InitialBalanceDto
{
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BalanceDirection { get; set; } = string.Empty; // "Debit" / "Credit"
    public int Level { get; set; }
    public decimal DebitBalance { get; set; }
    public decimal CreditBalance { get; set; }
    public bool IsLeaf { get; set; }
}

public class SaveInitialBalancesRequest
{
    public long AccountSetId { get; set; }
    public List<InitialBalanceItem> Items { get; set; } = new();
}

// 保留旧名称兼容
public class SaveInitialBalanceRequest
{
    public long PeriodId { get; set; }
    public List<InitialBalanceItem> Balances { get; set; } = new();
}

public class InitialBalanceItem
{
    public long AccountId { get; set; }
    public decimal DebitBalance { get; set; }
    public decimal CreditBalance { get; set; }
}

/// <summary>
/// 批量更新科目辅助核算关联请求
/// </summary>
public class UpdateAccountAuxiliaryRequest
{
    /// <summary>账套ID</summary>
    public long AccountSetId { get; set; }

    /// <summary>辅助核算类别（如 customer/supplier/employee...）</summary>
    public string AuxType { get; set; } = string.Empty;

    /// <summary>需要关联该辅助类别的科目编码列表</summary>
    public List<string> AccountCodes { get; set; } = new();
}
