using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmTaskMember : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FTaskId { get; set; }
    public long FUserId { get; set; }
    public int FRole { get; set; }

    // 导航属性
    public virtual TmTask? Task { get; set; }
}
