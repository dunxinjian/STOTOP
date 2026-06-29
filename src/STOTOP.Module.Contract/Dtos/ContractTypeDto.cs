namespace STOTOP.Module.Contract.Dtos;

/// <summary>
/// 合同类型详情 DTO
/// </summary>
public class ContractTypeDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public int Status { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? UpdaterName { get; set; }
    public DateTime? UpdatedTime { get; set; }
}

/// <summary>
/// 创建合同类型请求
/// </summary>
public class CreateContractTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 更新合同类型请求
/// </summary>
public class UpdateContractTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 合同类型查询请求
/// </summary>
public class ContractTypeQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? Status { get; set; }
}
