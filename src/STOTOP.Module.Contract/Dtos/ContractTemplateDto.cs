namespace STOTOP.Module.Contract.Dtos;

/// <summary>
/// 合同模板详情 DTO
/// </summary>
public class ContractTemplateDto
{
    public long Id { get; set; }
    public long TypeId { get; set; }
    public string? TypeName { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? TemplateContent { get; set; }
    public int Version { get; set; }
    public int Status { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? UpdaterName { get; set; }
    public DateTime? UpdatedTime { get; set; }
}

/// <summary>
/// 合同模板列表项 DTO
/// </summary>
public class ContractTemplateListItemDto
{
    public long Id { get; set; }
    public long TypeId { get; set; }
    public string? TypeName { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public int Version { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建合同模板请求
/// </summary>
public class CreateContractTemplateRequest
{
    public long TypeId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? TemplateContent { get; set; }
}

/// <summary>
/// 更新合同模板请求
/// </summary>
public class UpdateContractTemplateRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public string? TemplateContent { get; set; }
}

/// <summary>
/// 合同模板查询请求
/// </summary>
public class ContractTemplateQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public long? TypeId { get; set; }
    public int? Status { get; set; }
}
