using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmReviewRecord : BaseEntity, IOrgScoped, ITenantScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public int FRelationType { get; set; }
    public long FRelationId { get; set; }
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
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
