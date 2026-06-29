namespace STOTOP.Module.Quality.Dtos;

/// <summary>
/// 异常详情DTO
/// </summary>
public class ExceptionDetailDto
{
    public long Id { get; set; }
    public string ExceptionNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Type { get; set; }
    public string TypeText { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string PriorityText { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? RelatedModule { get; set; }
    public long? RelatedEntityId { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public int? DispatchMethod { get; set; }
    public long? DispatchEntityId { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? ClosedTime { get; set; }
    public long CreatorId { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<ExceptionLogDto> Logs { get; set; } = new();
}

/// <summary>
/// 异常处理日志DTO
/// </summary>
public class ExceptionLogDto
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public int? FromStatus { get; set; }
    public int? ToStatus { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}
