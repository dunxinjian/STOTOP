using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Dtos;

/// <summary>
/// 异常列表DTO
/// </summary>
public class ExceptionListDto
{
    public long Id { get; set; }
    public string ExceptionNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Type { get; set; }
    public string TypeText { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string PriorityText { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime? Deadline { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 异常列表查询请求
/// </summary>
public class ExceptionPagedRequest : PagedRequest
{
    public int? Type { get; set; }
    public int? Status { get; set; }
    public int? Priority { get; set; }
    public long? AssigneeId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// 创建异常单请求
/// </summary>
public class CreateExceptionRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Type { get; set; }
    public int Priority { get; set; }
    public string? Source { get; set; }
    public string? RelatedModule { get; set; }
    public long? RelatedEntityId { get; set; }
    public DateTime? Deadline { get; set; }
}

/// <summary>
/// 更新异常单请求
/// </summary>
public class UpdateExceptionRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? Priority { get; set; }
    public DateTime? Deadline { get; set; }
}

/// <summary>
/// 派发异常单请求
/// </summary>
public class DispatchExceptionRequest
{
    public long AssigneeId { get; set; }
    public int DispatchMethod { get; set; }
    public string? Remark { get; set; }
    public int? TimeoutHours { get; set; }
}

/// <summary>
/// 关闭异常单请求
/// </summary>
public class CloseExceptionRequest
{
    public string? Result { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 转派请求
/// </summary>
public class ReassignExceptionRequest
{
    public long NewAssigneeId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// 异常状态统计
/// </summary>
public class ExceptionCountByStatusDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Processing { get; set; }
    public int Overdue { get; set; }
    public int Closed { get; set; }
}
