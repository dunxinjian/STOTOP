using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 改进措施
/// </summary>
public class QlReviewImprovement : BaseEntity
{
    public long FReviewId { get; set; }
    public string FContent { get; set; } = string.Empty;
    public long? FAssigneeId { get; set; }
    public DateTime? FDeadline { get; set; }
    public bool FCompleted { get; set; }
    public DateTime? FCompletedTime { get; set; }
    public int FSortOrder { get; set; }
}
