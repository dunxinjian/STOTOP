using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTaskSchedule : BaseEntity
{
    public long FTemplateTaskId { get; set; }
    public int FScheduleType { get; set; }
    public string? FCronExpression { get; set; }
    public DateTime? FScheduledTime { get; set; }
    public DateTime? FNextExecution { get; set; }
    public DateTime? FLastExecution { get; set; }
    public bool FIsEnabled { get; set; } = true;
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTask? TemplateTask { get; set; }
}
