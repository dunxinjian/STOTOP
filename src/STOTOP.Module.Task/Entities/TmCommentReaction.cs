using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmCommentReaction : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FCommentId { get; set; }
    public long FUserId { get; set; }
    public string FEmojiCode { get; set; } = string.Empty;
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTaskComment? Comment { get; set; }
}
