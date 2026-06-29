using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 知识库文章
/// </summary>
public class QlKnowledge : BaseEntity
{
    public long FOrgId { get; set; }
    public string FTitle { get; set; } = string.Empty;
    public string FContent { get; set; } = string.Empty;
    public string? FCategory { get; set; }
    public string? FTags { get; set; }
    public long? FRelatedExceptionId { get; set; }
    public long? FRelatedReviewId { get; set; }
    public int FViewCount { get; set; }
    public long FCreatorId { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
