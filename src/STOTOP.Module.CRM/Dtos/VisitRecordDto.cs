namespace STOTOP.Module.CRM.Dtos;

/// <summary>
/// 拜访记录详情DTO
/// </summary>
public class VisitRecordDto
{
    public long Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public long VisitorId { get; set; }
    public DateOnly VisitDate { get; set; }
    public int VisitMethod { get; set; }
    public string? Content { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 拜访记录列表项DTO
/// </summary>
public class VisitRecordListItemDto
{
    public long Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public long VisitorId { get; set; }
    public DateOnly VisitDate { get; set; }
    public int VisitMethod { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建拜访记录请求
/// </summary>
public class CreateVisitRecordRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public long VisitorId { get; set; }
    public DateOnly VisitDate { get; set; }
    /// <summary>
    /// 拜访方式：1=上门, 2=电话, 3=微信, 4=其他
    /// </summary>
    public int VisitMethod { get; set; }
    public string? Content { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
}

/// <summary>
/// 更新拜访记录请求
/// </summary>
public class UpdateVisitRecordRequest
{
    public DateOnly VisitDate { get; set; }
    public int VisitMethod { get; set; }
    public string? Content { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
}

/// <summary>
/// 拜访记录查询请求
/// </summary>
public class VisitRecordQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? CustomerId { get; set; }
    public long? VisitorId { get; set; }
    public int? VisitMethod { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

/// <summary>
/// 拜访统计DTO
/// </summary>
public class VisitStatisticsDto
{
    public int TotalVisits { get; set; }
    public int TodayVisits { get; set; }
    public int WeekVisits { get; set; }
    public int MonthVisits { get; set; }
    public int PendingFollowUp { get; set; }
}
