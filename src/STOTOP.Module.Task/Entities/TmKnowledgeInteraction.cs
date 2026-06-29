using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmKnowledgeInteraction : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FKnowledgeId { get; set; }
    public long FUserId { get; set; }
    public int FInteractionType { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmKnowledge? Knowledge { get; set; }
}
