using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Dtos;

/// <summary>
/// 排行榜 - 列表项 DTO
/// </summary>
public class RankingListDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public string? DepartmentName { get; set; }
    public int TotalPoints { get; set; }
    public int AwardPoints { get; set; }
    public int DeductPoints { get; set; }
    public int Rank { get; set; }
    public string Period { get; set; } = string.Empty;
    public int Dimension { get; set; }
}

/// <summary>
/// 部门排名 DTO
/// </summary>
public class DepartmentRankingDto
{
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int AwardPoints { get; set; }
    public int DeductPoints { get; set; }
    public int MemberCount { get; set; }
    public decimal AvgPoints { get; set; }
    public int Rank { get; set; }
}

/// <summary>
/// 我的排名 DTO
/// </summary>
public class MyRankingDto
{
    public int TotalPoints { get; set; }
    public int AwardPoints { get; set; }
    public int DeductPoints { get; set; }
    public int Rank { get; set; }
    public int TotalUsers { get; set; }
    public string Period { get; set; } = string.Empty;
    public int Dimension { get; set; }
    /// <summary>排名趋势（近几个周期的排名变化）</summary>
    public List<RankTrendItem> Trends { get; set; } = new();
}

/// <summary>
/// 排名趋势数据项
/// </summary>
public class RankTrendItem
{
    public string Period { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int Rank { get; set; }
}

/// <summary>
/// 排行榜查询请求（支持月度/季度/年度维度）
/// </summary>
public class RankingPagedRequest : PagedRequest
{
    /// <summary>维度：0月度/1季度/2年度</summary>
    public int Dimension { get; set; }
    /// <summary>周期标识（如 2026-04、2026-Q1、2026）</summary>
    public string? Period { get; set; }
}

/// <summary>
/// 部门排名查询请求
/// </summary>
public class DepartmentRankingRequest
{
    /// <summary>维度：0月度/1季度/2年度</summary>
    public int Dimension { get; set; }
    /// <summary>周期标识</summary>
    public string? Period { get; set; }
}
