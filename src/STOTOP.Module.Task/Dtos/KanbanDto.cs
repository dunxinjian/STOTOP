namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 看板数据DTO（按状态分组的任务列表）
/// </summary>
public class KanbanDataDto
{
    public long? ProjectId { get; set; }
    public List<KanbanColumnDto> Columns { get; set; } = new();
}

/// <summary>
/// 看板列DTO（按状态分组）
/// </summary>
public class KanbanColumnDto
{
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<KanbanCardDto> Cards { get; set; } = new();
}

/// <summary>
/// 看板卡片DTO
/// </summary>
public class KanbanCardDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int Priority { get; set; }
    public int Status { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime? PlanEnd { get; set; }
    public int Progress { get; set; }
    public int SubTaskCount { get; set; }
    public int CompletedSubTaskCount { get; set; }
    public int Sort { get; set; }
    public List<TagSimpleDto> Tags { get; set; } = new();
}

/// <summary>
/// 看板拖拽移动请求（含目标状态+排序值）
/// </summary>
public class KanbanMoveRequest
{
    public long TaskId { get; set; }
    public int TargetStatus { get; set; }
    public int TargetSort { get; set; }
}

/// <summary>
/// 看板查询请求
/// </summary>
public class KanbanQueryRequest
{
    public long? ProjectId { get; set; }
    public long? AssigneeId { get; set; }
    public List<long>? TagIds { get; set; }
    public int? Priority { get; set; }
}
