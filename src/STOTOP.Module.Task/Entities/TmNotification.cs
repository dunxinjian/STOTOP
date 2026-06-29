using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmNotification : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FReceiverId { get; set; }
    public int FEventType { get; set; }
    public string FTitle { get; set; } = string.Empty;
    public string FContent { get; set; } = string.Empty;
    public int FRelationType { get; set; }
    public long FRelationId { get; set; }
    public bool FIsRead { get; set; } = false;
    public bool FPushedToDingTalk { get; set; } = false;
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
