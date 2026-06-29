using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmKnowledge : BaseEntity, IOrgScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public string FTitle { get; set; } = string.Empty;
    public string? FContent { get; set; }
    public int FCategory { get; set; }
    public long FOrgId { get; set; }
    public long FAuthorId { get; set; }
    public long? FSourceReviewId { get; set; }
    public long? FSourceTaskId { get; set; }
    public long? FSourceProjectId { get; set; }
    public int FViewCount { get; set; } = 0;
    public int FLikeCount { get; set; } = 0;
    public int FCollectCount { get; set; } = 0;
    public int FStatus { get; set; } = 0;
    public bool FIsPinned { get; set; } = false;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<TmKnowledgeInteraction> Interactions { get; set; } = new List<TmKnowledgeInteraction>();
    public virtual ICollection<TmKnowledgeComment> Comments { get; set; } = new List<TmKnowledgeComment>();
}
