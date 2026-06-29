namespace STOTOP.Module.Contract.Dtos;

/// <summary>
/// 合同详情 DTO（含合同方、条款等）
/// </summary>
public class ContractDto
{
    public long Id { get; set; }
    public string ContractNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long TypeId { get; set; }
    public string? TypeName { get; set; }
    public long? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public long? RelatedContractId { get; set; }
    public string? RelatedContractNo { get; set; }
    public int ContractNature { get; set; }
    public int Status { get; set; }
    public long? OaProcessInstanceId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? UpdaterName { get; set; }
    public DateTime? UpdatedTime { get; set; }
    public List<ContractPartyDto> Parties { get; set; } = new();
    public List<ContractClauseDto> Clauses { get; set; } = new();
    public List<ContractReminderDto> Reminders { get; set; } = new();
    public List<ESignRecordDto> ESignRecords { get; set; } = new();
}

/// <summary>
/// 合同列表项 DTO
/// </summary>
public class ContractListItemDto
{
    public long Id { get; set; }
    public string ContractNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long TypeId { get; set; }
    public string? TypeName { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int ContractNature { get; set; }
    public int Status { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 合同方 DTO
/// </summary>
public class ContractPartyDto
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public int PartyRole { get; set; }
    public string? RelatedBusinessType { get; set; }
    public long? RelatedBusinessId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

/// <summary>
/// 合同条款 DTO
/// </summary>
public class ContractClauseDto
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public int ClauseOrder { get; set; }
    public string ClauseTitle { get; set; } = string.Empty;
    public string? ClauseContent { get; set; }
    public bool IsKeyClause { get; set; }
}

/// <summary>
/// 创建合同请求
/// </summary>
public class CreateContractRequest
{
    public string ContractNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long TypeId { get; set; }
    public long? TemplateId { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public long? RelatedContractId { get; set; }
    public int ContractNature { get; set; } = 1;
    public List<CreateContractPartyRequest>? Parties { get; set; }
    public List<CreateContractClauseRequest>? Clauses { get; set; }
}

/// <summary>
/// 更新合同请求
/// </summary>
public class UpdateContractRequest
{
    public string Title { get; set; } = string.Empty;
    public long TypeId { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<CreateContractPartyRequest>? Parties { get; set; }
    public List<CreateContractClauseRequest>? Clauses { get; set; }
}

/// <summary>
/// 创建合同方请求
/// </summary>
public class CreateContractPartyRequest
{
    public int PartyRole { get; set; }
    public string? RelatedBusinessType { get; set; }
    public long? RelatedBusinessId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

/// <summary>
/// 创建合同条款请求
/// </summary>
public class CreateContractClauseRequest
{
    public int ClauseOrder { get; set; }
    public string ClauseTitle { get; set; } = string.Empty;
    public string? ClauseContent { get; set; }
    public bool IsKeyClause { get; set; }
}

/// <summary>
/// 更新合同状态请求
/// </summary>
public class UpdateContractStatusRequest
{
    public int Status { get; set; }
}

/// <summary>
/// 合同查询请求
/// </summary>
public class ContractQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public long? TypeId { get; set; }
    public int? Status { get; set; }
    public int? ContractNature { get; set; }
}

/// <summary>合同状态统计</summary>
public class ContractStatisticsDto
{
    public int TotalCount { get; set; }
    public List<ContractStatusGroupDto> ByStatus { get; set; } = new();
}

/// <summary>按状态分组统计</summary>
public class ContractStatusGroupDto
{
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
}
