namespace STOTOP.WebAPI.Dtos.WorkHub;

public class WorkItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;      // "oa", "quality", "task", etc.
    public string Category { get; set; } = string.Empty;    // "approval", "task", "alert", "notification", "reminder"

    /// <summary>业务类型键（用于前端上色/分组，如 "voucher"/"quality"/"flow:费用报销"）。</summary>
    public string BizTypeKey { get; set; } = "approval";

    /// <summary>业务类型中文标签（用户可见，如 "凭证复核"/"质量异常"/"费用报销"）。</summary>
    public string BizTypeLabel { get; set; } = "审批";
    public string Priority { get; set; } = "normal";        // "urgent", "high", "normal", "low"
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public DateTime? Deadline { get; set; }
    public List<WorkItemActionDto> Actions { get; set; } = new();
    /// <summary>
    /// 关联的相关业务链接（侧栏“相关业务”等位置使用）
    /// </summary>
    public List<RelatedLinkDto> RelatedLinks { get; set; } = new();
    public string? ConversationSessionCode { get; set; }
    public string? DetailRoute { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class WorkItemActionDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>
    /// 操作样式分层：'primary' | 'secondary' | 'default' | 'danger'
    /// </summary>
    public string? Type { get; set; }
    /// <summary>
    /// 是否为终结性操作（执行后任务进入归档/不可逆状态）
    /// </summary>
    public bool? Finalizes { get; set; }
    /// <summary>
    /// 执行前是否需要二次确认
    /// </summary>
    public bool? NeedsConfirm { get; set; }
    /// <summary>
    /// 二次确认弹窗中的影响摘要清单
    /// </summary>
    public List<string>? ConfirmSummary { get; set; }
    public string? Route { get; set; }
    public string? ConversationSessionCode { get; set; }
}

/// <summary>
/// 工作项相关链接
/// </summary>
public class RelatedLinkDto
{
    public string Label { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Summary { get; set; }
    public string? Permission { get; set; }
}

public class WorkHubStatsDto
{
    public int Total { get; set; }
    public int Approval { get; set; }
    public int Task { get; set; }
    public int Alert { get; set; }
    public int Notification { get; set; }
    public int Reminder { get; set; }
    public int Initiated { get; set; }
}
