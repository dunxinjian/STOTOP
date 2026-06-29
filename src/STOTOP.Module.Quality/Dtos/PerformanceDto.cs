using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Dtos;

/// <summary>
/// 绩效DTO
/// </summary>
public class PerformanceDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public int ExceptionCount { get; set; }
    public int ResolvedCount { get; set; }
    public int OverdueCount { get; set; }
    public decimal Score { get; set; }
    public string? Remark { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 绩效查询请求
/// </summary>
public class PerformancePagedRequest : PagedRequest
{
    public long? UserId { get; set; }
    public string? Period { get; set; }
}

/// <summary>
/// 绩效统计DTO
/// </summary>
public class PerformanceStatsDto
{
    public double AvgScore { get; set; }
    public double MaxScore { get; set; }
    public int TotalHandled { get; set; }
    public double OverdueRate { get; set; }
    public int MyRank { get; set; }
    public int TotalUsers { get; set; }
}

/// <summary>
/// 绩效排名DTO
/// </summary>
public class PerformanceRankingDto
{
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public double Score { get; set; }
    public int ExceptionCount { get; set; }
    public int ResolvedCount { get; set; }
    public int OverdueCount { get; set; }
}

/// <summary>
/// 绩效趋势DTO
/// </summary>
public class PerformanceTrendDto
{
    public string Period { get; set; } = string.Empty;
    public double Score { get; set; }
    public int HandleCount { get; set; }
}
