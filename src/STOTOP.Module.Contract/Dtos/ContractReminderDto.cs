namespace STOTOP.Module.Contract.Dtos;

/// <summary>
/// 合同提醒详情 DTO
/// </summary>
public class ContractReminderDto
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public string? ContractNo { get; set; }
    public string? ContractTitle { get; set; }
    public int ReminderType { get; set; }
    public DateTime ReminderDate { get; set; }
    public long RecipientId { get; set; }
    public bool IsHandled { get; set; }
    public string? Remark { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建合同提醒请求
/// </summary>
public class CreateContractReminderRequest
{
    public long ContractId { get; set; }
    public int ReminderType { get; set; }
    public DateTime ReminderDate { get; set; }
    public long RecipientId { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新合同提醒请求
/// </summary>
public class UpdateContractReminderRequest
{
    public int ReminderType { get; set; }
    public DateTime ReminderDate { get; set; }
    public long RecipientId { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 合同提醒查询请求
/// </summary>
public class ContractReminderQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? ContractId { get; set; }
    public long? RecipientId { get; set; }
    public bool? IsHandled { get; set; }
}
