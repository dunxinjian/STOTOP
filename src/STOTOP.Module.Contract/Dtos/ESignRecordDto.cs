namespace STOTOP.Module.Contract.Dtos;

/// <summary>
/// 电子签记录详情 DTO
/// </summary>
public class ESignRecordDto
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public string? ContractNo { get; set; }
    public string Signer { get; set; } = string.Empty;
    public string? SignerRole { get; set; }
    public string? SignMethod { get; set; }
    public int SignStatus { get; set; }
    public DateTime? SignedTime { get; set; }
    public string? ThirdPartyNo { get; set; }
    public string? SignedFilePath { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建电子签记录请求
/// </summary>
public class CreateESignRecordRequest
{
    public long ContractId { get; set; }
    public string Signer { get; set; } = string.Empty;
    public string? SignerRole { get; set; }
    public string? SignMethod { get; set; }
}

/// <summary>
/// 手动签署请求（ManualSignProvider）
/// </summary>
public class ManualSignRequest
{
    public string? SignedFilePath { get; set; }
}

/// <summary>
/// 电子签记录查询请求
/// </summary>
public class ESignRecordQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? ContractId { get; set; }
    public int? SignStatus { get; set; }
}
