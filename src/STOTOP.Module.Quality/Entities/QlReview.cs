using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 复盘记录
/// </summary>
public class QlReview : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FExceptionId { get; set; }
    public string FTitle { get; set; } = string.Empty;
    public string? FRootCause { get; set; }
    public string? FImpactAnalysis { get; set; }
    public string? FConclusion { get; set; }
    public long FCreatorId { get; set; }
    public DateTime FReviewDate { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
