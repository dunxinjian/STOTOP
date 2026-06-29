using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTaskComment : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FTaskId { get; set; }
    public long FUserId { get; set; }
    public string FContent { get; set; } = string.Empty;
    public int FType { get; set; } = 0;
    public long FParentCommentId { get; set; } = 0;
    public bool FPushedToDingTalk { get; set; } = false;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTask? Task { get; set; }
    public virtual ICollection<TmCommentReaction> Reactions { get; set; } = new List<TmCommentReaction>();
}
