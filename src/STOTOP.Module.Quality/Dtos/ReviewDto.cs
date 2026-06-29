using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Dtos;

/// <summary>
/// 复盘DTO
/// </summary>
public class ReviewDto
{
    public long Id { get; set; }
    public long ExceptionId { get; set; }
    public string ExceptionTitle { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? RootCause { get; set; }
    public string? ImpactAnalysis { get; set; }
    public string? Conclusion { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public DateTime ReviewDate { get; set; }
    public DateTime CreateTime { get; set; }
    public List<ReviewImprovementDto> Improvements { get; set; } = new();
}

/// <summary>
/// 改进措施DTO
/// </summary>
public class ReviewImprovementDto
{
    public long Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime? Deadline { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedTime { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 复盘查询请求
/// </summary>
public class ReviewPagedRequest : PagedRequest
{
    public long? ExceptionId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// 创建复盘请求
/// </summary>
public class CreateReviewRequest
{
    public long ExceptionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? RootCause { get; set; }
    public string? ImpactAnalysis { get; set; }
    public string? Conclusion { get; set; }
    public DateTime ReviewDate { get; set; }
    public List<ReviewImprovementDto>? Improvements { get; set; }
}

/// <summary>
/// 更新复盘请求
/// </summary>
public class UpdateReviewRequest
{
    public string? Title { get; set; }
    public string? RootCause { get; set; }
    public string? ImpactAnalysis { get; set; }
    public string? Conclusion { get; set; }
    public List<ReviewImprovementDto>? Improvements { get; set; }
}

/// <summary>
/// 复盘统计DTO
/// </summary>
public class ReviewStatsDto
{
    public int TotalReviews { get; set; }
    public int MonthNewCount { get; set; }
    public int PendingImprovements { get; set; }
    public double CompletionRate { get; set; }
}

/// <summary>
/// 改进措施查询请求
/// </summary>
public class ImprovementPagedRequest : PagedRequest
{
    public bool? Completed { get; set; }
    public long? ReviewId { get; set; }
    public long? AssigneeId { get; set; }
}

/// <summary>
/// 改进措施列表项DTO
/// </summary>
public class ImprovementListDto
{
    public long Id { get; set; }
    public long ReviewId { get; set; }
    public string ReviewTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime? Deadline { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedTime { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 更新改进措施请求
/// </summary>
public class UpdateImprovementRequest
{
    public string? Content { get; set; }
    public long? AssigneeId { get; set; }
    public DateTime? Deadline { get; set; }
}

/// <summary>
/// 完成改进措施请求
/// </summary>
public class CompleteImprovementRequest
{
    public string? EffectDescription { get; set; }
}
