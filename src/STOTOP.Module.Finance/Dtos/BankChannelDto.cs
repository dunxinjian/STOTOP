namespace STOTOP.Module.Finance.Dtos;

/// <summary>
/// 交易渠道详情 DTO
/// </summary>
public class BankChannelDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public string? AccountNo { get; set; }
    public string? BankName { get; set; }
    public string? ImportTemplate { get; set; }
    public int Status { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? UpdaterName { get; set; }
    public DateTime? UpdatedTime { get; set; }
}

/// <summary>
/// 创建交易渠道请求
/// </summary>
public class CreateBankChannelRequest
{
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public string? AccountNo { get; set; }
    public string? BankName { get; set; }
    public string? ImportTemplate { get; set; }
}

/// <summary>
/// 更新交易渠道请求
/// </summary>
public class UpdateBankChannelRequest
{
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public string? AccountNo { get; set; }
    public string? BankName { get; set; }
    public string? ImportTemplate { get; set; }
    public int Status { get; set; }
}

/// <summary>
/// 交易渠道查询请求
/// </summary>
public class BankChannelQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? Status { get; set; }
}

/// <summary>
/// 银行账户选择器轻量 DTO（供 CardFlow 等场景使用）
/// </summary>
public class BankAccountSelectorDto
{
    public long Id { get; set; }
    public string AccountNo { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public string? BankName { get; set; }
    public long? AccountId { get; set; }
    public string? AccountCode { get; set; }
}
