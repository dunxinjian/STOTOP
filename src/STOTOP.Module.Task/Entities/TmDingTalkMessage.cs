using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmDingTalkMessage : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public int FSourceType { get; set; }
    public long FSourceId { get; set; }
    public long FTaskId { get; set; }
    public long FSenderId { get; set; }
    public int FPushStatus { get; set; } = 0;
    public string? FDingTalkMessageId { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTask? Task { get; set; }
}
