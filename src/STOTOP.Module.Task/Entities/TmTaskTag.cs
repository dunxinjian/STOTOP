using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTaskTag : BaseEntity
{
    public long FTaskId { get; set; }
    public long FTagId { get; set; }

    // 导航属性
    public virtual TmTask? Task { get; set; }
    public virtual TmTag? Tag { get; set; }
}
