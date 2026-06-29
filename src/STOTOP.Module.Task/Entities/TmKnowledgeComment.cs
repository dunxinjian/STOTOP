using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmKnowledgeComment : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FKnowledgeId { get; set; }
    public long FUserId { get; set; }
    public string FContent { get; set; } = string.Empty;
    public long FParentCommentId { get; set; } = 0;
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmKnowledge? Knowledge { get; set; }
}
