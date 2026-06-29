using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTaskVisibility : BaseEntity
{
    public long FTaskId { get; set; }
    public int FTargetType { get; set; }
    public long FTargetId { get; set; }

    // 导航属性
    public virtual TmTask? Task { get; set; }
}
