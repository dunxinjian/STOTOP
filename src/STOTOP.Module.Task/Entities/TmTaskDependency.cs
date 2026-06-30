using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTaskDependency : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FTaskId { get; set; }
    public long FDependsOnTaskId { get; set; }
    public int FDependencyType { get; set; } = 0;

    // 导航属性
    public virtual TmTask? Task { get; set; }
    public virtual TmTask? DependsOnTask { get; set; }
}
