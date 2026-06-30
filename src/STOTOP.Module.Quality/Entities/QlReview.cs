using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 复盘记录
/// </summary>
public class QlReview : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
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
