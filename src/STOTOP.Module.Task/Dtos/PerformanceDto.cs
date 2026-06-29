using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

// ===== 考核周期 =====

/// <summary>
/// 考核周期列表DTO
/// </summary>
public class PerformancePeriodListDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long OrgId { get; set; }
    public int Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Status { get; set; }
    public int RecordCount { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 创建考核周期请求
/// </summary>
public class CreatePerformancePeriodRequest
{
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// <summary>
/// 更新考核周期请求
/// </summary>
public class UpdatePerformancePeriodRequest
{
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Status { get; set; }
}

/// <summary>
/// 考核周期分页查询请求
/// </summary>
public class PerformancePeriodPagedRequest : PagedRequest
{
    public int? Type { get; set; }
    public int? Status { get; set; }
}

// ===== 考核记录 =====

/// <summary>
/// 考核记录列表DTO
/// </summary>
public class PerformanceRecordListDto
{
    public long Id { get; set; }
    public long PeriodId { get; set; }
    public string? PeriodName { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public long OrgId { get; set; }
    public int TaskTotal { get; set; }
    public int CompletedCount { get; set; }
    public int OnTimeCount { get; set; }
    public int OverdueCount { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal OnTimeRate { get; set; }
    public decimal GoalAchievementRate { get; set; }
    public decimal? OverallScore { get; set; }
    public string? Grade { get; set; }
    public int Status { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 考核记录详情DTO（含任务明细+维度评分）
/// </summary>
public class PerformanceRecordDetailDto
{
    public long Id { get; set; }
    public long PeriodId { get; set; }
    public string? PeriodName { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public long OrgId { get; set; }
    public int TaskTotal { get; set; }
    public int CompletedCount { get; set; }
    public int OnTimeCount { get; set; }
    public int OverdueCount { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal OnTimeRate { get; set; }
    public decimal GoalAchievementRate { get; set; }
    public decimal? QualityScore { get; set; }
    public decimal? SelfScore { get; set; }
    public decimal? OverallScore { get; set; }
    public string? Grade { get; set; }
    public string? Comment { get; set; }
    public string? SelfComment { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<PerformanceScoreDto> DimensionScores { get; set; } = new();
}

/// <summary>
/// 维度评分DTO
/// </summary>
public class PerformanceScoreDto
{
    public long Id { get; set; }
    public long RecordId { get; set; }
    public long DimensionId { get; set; }
    public string? DimensionName { get; set; }
    public string? DimensionCode { get; set; }
    public int DataSource { get; set; }
    public int Weight { get; set; }
    public decimal MaxScore { get; set; }
    public decimal? Score { get; set; }
    public string? Evaluator { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 自评请求DTO
/// </summary>
public class SelfEvaluateRequest
{
    public string? SelfComment { get; set; }
    public List<DimensionScoreInput> DimensionScores { get; set; } = new();
}

/// <summary>
/// 上级评分请求DTO（含等级）
/// </summary>
public class SuperiorReviewRequest
{
    public string? Comment { get; set; }
    public string? Grade { get; set; }
    public List<DimensionScoreInput> DimensionScores { get; set; } = new();
}

/// <summary>
/// 维度评分输入
/// </summary>
public class DimensionScoreInput
{
    public long DimensionId { get; set; }
    public decimal Score { get; set; }
    public string? Remark { get; set; }
}

// ===== 评价维度配置 =====

/// <summary>
/// 评价维度配置列表DTO
/// </summary>
public class PerformanceDimensionListDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public string DimensionName { get; set; } = string.Empty;
    public string DimensionCode { get; set; } = string.Empty;
    public int DataSource { get; set; }
    public int Weight { get; set; }
    public decimal MaxScore { get; set; }
    public int Sort { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 创建评价维度请求
/// </summary>
public class CreatePerformanceDimensionRequest
{
    public string DimensionName { get; set; } = string.Empty;
    public string DimensionCode { get; set; } = string.Empty;
    public int DataSource { get; set; }
    public int Weight { get; set; } = 100;
    public decimal MaxScore { get; set; } = 100;
    public int Sort { get; set; } = 0;
}

/// <summary>
/// 更新评价维度请求
/// </summary>
public class UpdatePerformanceDimensionRequest
{
    public string DimensionName { get; set; } = string.Empty;
    public string DimensionCode { get; set; } = string.Empty;
    public int DataSource { get; set; }
    public int Weight { get; set; } = 100;
    public decimal MaxScore { get; set; } = 100;
    public int Sort { get; set; } = 0;
    public bool IsEnabled { get; set; } = true;
}

// ===== 绩效看板统计 =====

/// <summary>
/// 绩效看板统计DTO
/// </summary>
public class PerformanceDashboardDto
{
    public long PeriodId { get; set; }
    public string? PeriodName { get; set; }
    public int TotalEmployees { get; set; }
    public int EvaluatedCount { get; set; }
    public int PendingSelfCount { get; set; }
    public int PendingReviewCount { get; set; }
    public decimal AvgCompletionRate { get; set; }
    public decimal AvgOnTimeRate { get; set; }
    public decimal AvgOverallScore { get; set; }
    public List<GradeDistributionDto> GradeDistribution { get; set; } = new();
}

/// <summary>
/// 考核等级分布DTO
/// </summary>
public class GradeDistributionDto
{
    public string Grade { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}
