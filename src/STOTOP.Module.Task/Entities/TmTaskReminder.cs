using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTaskReminder : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FTaskId { get; set; }
    public long FUserId { get; set; }
    public DateTime FReminderTime { get; set; }
    public int FReminderType { get; set; }
    public bool FIsRead { get; set; } = false;
    public bool FIsSent { get; set; } = false;
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTask? Task { get; set; }
}
