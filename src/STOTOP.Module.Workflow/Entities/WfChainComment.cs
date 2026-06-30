using STOTOP.Core.Models;

namespace STOTOP.Module.Workflow.Entities;

/// <summary>WF链路评论 - 参与人协作信息发布</summary>
public class WfChainComment : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string FChainId { get; set; } = string.Empty;
    public long? FWorkItemId { get; set; }
    public long FAuthorId { get; set; }
    public string? FAuthorName { get; set; }
    public string FContent { get; set; } = string.Empty;
    public string? FAttachments { get; set; }
    public long? FReplyToId { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public bool FIsDeleted { get; set; } = false;

    // 导航属性
    public virtual WfChainComment? ReplyTo { get; set; }
}
