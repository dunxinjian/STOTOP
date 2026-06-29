using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmReviewRecord : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public int FRelationType { get; set; }
    public long FRelationId { get; set; }
    public long FOrgId { get; set; }
    public string FTitle { get; set; } = string.Empty;
    public string? FWentWell { get; set; }
    public string? FToImprove { get; set; }
    public string? FLessonsLearned { get; set; }
    public string? FActionPlan { get; set; }
    public long FReviewerId { get; set; }
    public string? FParticipantIds { get; set; }
    public int FStatus { get; set; } = 0;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
