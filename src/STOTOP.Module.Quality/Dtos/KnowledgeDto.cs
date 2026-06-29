using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Dtos;

/// <summary>
/// 知识库DTO
/// </summary>
public class KnowledgeDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public long? RelatedExceptionId { get; set; }
    public long? RelatedReviewId { get; set; }
    public int ViewCount { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 知识库查询请求
/// </summary>
public class KnowledgePagedRequest : PagedRequest
{
    public string? Category { get; set; }
    public string? Tag { get; set; }
}

/// <summary>
/// 创建知识库文章请求
/// </summary>
public class CreateKnowledgeRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public long? RelatedExceptionId { get; set; }
    public long? RelatedReviewId { get; set; }
}

/// <summary>
/// 更新知识库文章请求
/// </summary>
public class UpdateKnowledgeRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
}

/// <summary>
/// 知识库统计DTO
/// </summary>
public class KnowledgeStatsDto
{
    /// <summary>文章总数</summary>
    public int TotalArticles { get; set; }
    /// <summary>本月新增</summary>
    public int MonthNewCount { get; set; }
    /// <summary>各分类数量</summary>
    public List<DistributionItem> CategoryDistribution { get; set; } = new();
    /// <summary>热门标签(Top10)</summary>
    public List<TagCountItem> TopTags { get; set; } = new();
}

/// <summary>
/// 标签计数项
/// </summary>
public class TagCountItem
{
    public string Tag { get; set; } = string.Empty;
    public int Count { get; set; }
}
