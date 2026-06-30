using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmKnowledgeInteraction : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FKnowledgeId { get; set; }
    public long FUserId { get; set; }
    public int FInteractionType { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmKnowledge? Knowledge { get; set; }
}
