namespace STOTOP.Module.CardFlow.Dtos;

// ===== Dashboard Summary =====

public class QualityDashboardSummaryDto
{
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public int Resolved { get; set; }
    public int Overdue { get; set; }
    public List<QualityIssueTypeCountDto> ByType { get; set; } = new();
}

public class QualityIssueTypeCountDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

// ===== Dashboard Trend =====

public class QualityTrendDto
{
    public List<QualityTrendDayDto> Days { get; set; } = new();
}

public class QualityTrendDayDto
{
    public string Date { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Completed { get; set; }
}

// ===== Dashboard Workload =====

public class QualityWorkloadDto
{
    public List<QualityWorkloadItemDto> Items { get; set; } = new();
}

public class QualityWorkloadItemDto
{
    public long? AssigneeId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Total { get; set; }
}

// ===== Dashboard Overdue =====

public class QualityOverdueItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? BizType { get; set; }
    public long OrgId { get; set; }
    public string? AssigneeName { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? Deadline { get; set; }
}

// ===== WorkHub Quality Summary =====

public class WorkHubQualitySummaryDto
{
    public int PendingTotal { get; set; }
    public int TodayNew { get; set; }
    public int OverdueWarning { get; set; }
}
