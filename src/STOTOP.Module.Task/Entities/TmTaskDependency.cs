using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTaskDependency : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FTaskId { get; set; }
    public long FDependsOnTaskId { get; set; }
    public int FDependencyType { get; set; } = 0;

    // 导航属性
    public virtual TmTask? Task { get; set; }
    public virtual TmTask? DependsOnTask { get; set; }
}
