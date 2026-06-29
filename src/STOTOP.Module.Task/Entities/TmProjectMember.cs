using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmProjectMember : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FProjectId { get; set; }
    public long FUserId { get; set; }
    public int FRole { get; set; }
    public DateTime FJoinTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmProject? Project { get; set; }
}
