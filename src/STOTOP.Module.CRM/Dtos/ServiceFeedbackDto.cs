namespace STOTOP.Module.CRM.Dtos;

/// <summary>
/// 服务反馈详情DTO
/// </summary>
public class ServiceFeedbackDto
{
    public long Id { get; set; }
    public long SubmitterId { get; set; }
    public long? OrgId { get; set; }
    public string? CustomerId { get; set; }
    public long? OrderId { get; set; }
    public int Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Suggestion { get; set; }
    public string? Attachments { get; set; }
    public int Status { get; set; }
    public long? HandlerId { get; set; }
    public string? HandleResult { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? UpdaterName { get; set; }
    public DateTime? UpdatedTime { get; set; }
}

/// <summary>
/// 服务反馈列表项DTO
/// </summary>
public class ServiceFeedbackListItemDto
{
    public long Id { get; set; }
    public long SubmitterId { get; set; }
    public string? CustomerId { get; set; }
    public int Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public long? HandlerId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建服务反馈请求
/// </summary>
public class CreateServiceFeedbackRequest
{
    public long SubmitterId { get; set; }
    public long? OrgId { get; set; }
    public string? CustomerId { get; set; }
    public long? OrderId { get; set; }
    /// <summary>
    /// 反馈类别：1=服务质量, 2=时效问题, 3=费用争议, 4=建议, 5=其他
    /// </summary>
    public int Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Suggestion { get; set; }
    public string? Attachments { get; set; }
}

/// <summary>
/// 更新服务反馈请求
/// </summary>
public class UpdateServiceFeedbackRequest
{
    public int Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Suggestion { get; set; }
    public string? Attachments { get; set; }
}

/// <summary>
/// 反馈处理请求
/// </summary>
public class HandleFeedbackRequest
{
    public long HandlerId { get; set; }
    /// <summary>
    /// 目标状态：1=已受理, 2=改善中, 3=已落实, 4=已驳回
    /// </summary>
    public int NewStatus { get; set; }
    public string? HandleResult { get; set; }
}

/// <summary>
/// 服务反馈查询请求
/// </summary>
public class ServiceFeedbackQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? CustomerId { get; set; }
    public long? SubmitterId { get; set; }
    public int? Category { get; set; }
    public int? Status { get; set; }
}
