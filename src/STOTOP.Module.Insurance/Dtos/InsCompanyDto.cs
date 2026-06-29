namespace STOTOP.Module.Insurance.Dtos;

/// <summary>
/// 保险公司详情 DTO
/// </summary>
public class InsCompanyDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public int CompanyType { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 保险公司列表项 DTO
/// </summary>
public class InsCompanyListItemDto
{
    public long Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public int CompanyType { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public int Status { get; set; }
}

/// <summary>
/// 创建保险公司请求
/// </summary>
public class CreateInsCompanyRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public int CompanyType { get; set; } = 1;
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新保险公司请求
/// </summary>
public class UpdateInsCompanyRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public int CompanyType { get; set; } = 1;
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 保险公司查询请求
/// </summary>
public class InsCompanyQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? CompanyType { get; set; }
    public int? Status { get; set; }
}
